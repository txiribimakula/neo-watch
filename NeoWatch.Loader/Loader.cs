using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NeoWatch.Drawing;
using NeoWatch.Common;

namespace NeoWatch.Loading
{
    public class Loader
    {
        private IDebugger debugger;

        public IInterpreter Interpreter { get; set; }

        public Func<Task> YieldAction { get; set; } = async () => { await Task.Yield(); };

        public Loader(IDebugger debugger, IInterpreter interpreter)
        {
            this.debugger = debugger;
            Interpreter = interpreter;
        }

        private const int YieldEvery = 100;
        private static readonly TimeSpan MaxBetweenYields = TimeSpan.FromMilliseconds(100);

        public async Task<Result<Drawables>> Load(WatchItem item, CancellationToken cancellationToken = default(CancellationToken)) {

            IExpression expression = null;

            try
            {
                expression = debugger.GetExpression(item.Name);
            }
            catch (COMException)
            {
                return new Result<Drawables>(new Feedback(FeedbackType.ExpressionLoadException));
            }

            if (expression == null || string.IsNullOrEmpty(expression.Type))
            {
                return new Result<Drawables>(new Feedback(FeedbackType.ExpressionLoadException));
            }

            var drawablesResult = await GetDrawablesAsync(expression, item, cancellationToken);
            if(drawablesResult.Data != null)
            {
                drawablesResult.Data.Type = expression.Type;
            }

            return drawablesResult;
        }

        private async Task<Result<Drawables>> GetDrawablesAsync(IExpression itemExpression, WatchItem item, CancellationToken cancellationToken)
        {
            var drawables = new Drawables();

            var listTypes = new string[]
            {
                "std::vector",
                "std::array",
                "System.Collections.Generic.List"
            };

            var expressions = new ExpressionLoader(itemExpression, listTypes);

            item.LoadingTotal = EstimateTotal(itemExpression, listTypes);

            var sinceLastYield = Stopwatch.StartNew();
            var currentIndex = 0;
            foreach (IExpression expression in expressions)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return new Result<Drawables>(drawables, new Feedback(FeedbackType.Cancelled));
                }

                var innerExpressions = new ExpressionLoader(expression, listTypes);

                foreach (IExpression innerExpression in innerExpressions)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return new Result<Drawables>(drawables, new Feedback(FeedbackType.Cancelled));
                    }

                    var newDrawableResult = Interpreter.GetDrawable(innerExpression);

                    if (newDrawableResult.Feedback.HasError)
                    {
                        return new Result<Drawables>(drawables, newDrawableResult.Feedback);
                    }

                    newDrawableResult.Data.Description = "[" + drawables.Count + "]: " + newDrawableResult.Data.Description;
                    drawables.Add(newDrawableResult.Data);

                    currentIndex++;
                    if (currentIndex % YieldEvery == 0 || sinceLastYield.Elapsed > MaxBetweenYields)
                    {
                        item.LoadingCount = currentIndex;
                        await YieldAction();
                        sinceLastYield.Restart();
                    }
                }
            }

            return new Result<Drawables>(drawables);
        }

        private static int EstimateTotal(IExpression itemExpression, string[] listTypes)
        {
            // Best-effort O(1) estimate: for a flat list (the common case) DataMembers.Count
            // is exact. For nested lists we under-estimate; the count text remains useful
            // and the bar still grows as items load.
            var outer = new ExpressionLoader(itemExpression, listTypes);
            return outer.Any(e => !ReferenceEquals(e, itemExpression)) ? itemExpression.DataMembers.Count : 1;
        }
    }
}