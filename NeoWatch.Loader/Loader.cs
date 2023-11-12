using EnvDTE;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NeoWatch.Drawing;
using NeoWatch.Common;

namespace NeoWatch.Loading
{
    public class Loader
    {
        private readonly int MAX_SEGMENTS = 400;

        private Debugger debugger;

        public Interpreter Interpreter { get; set; }

        public Loader(Debugger debugger, Interpreter interpreter)
        {
            this.debugger = debugger;
            Interpreter = interpreter;
        }

        public async Task<Result<Drawables>> Load(WatchItem item) {

            Expression expression = null;

            try
            {
                expression = debugger.GetExpression(item.Name, true);
            }
            catch (COMException)
            {
                return new Result<Drawables>(null, "Variable could not be loaded");
            }

            if (expression == null || string.IsNullOrEmpty(expression.Type))
            {
                return new Result<Drawables>(null, "Variable could not be found");
            }

            var drawables = await GetDrawablesAsync(expression);
            drawables.Type = expression.Type;

            return new Result<Drawables>(drawables);
        }

        public Task<Drawables> GetDrawablesAsync(Expression expression)
        {
            return Task.Run(() => {
                return GetDrawables(expression);
            });
        }

        private Drawables GetDrawables(Expression itemExpression)
        {
            var drawables = new Drawables();

            var listTypes = new string[]
            {
                "std::vector",
                "std::array",
                "System.Collections.Generic.List"
            };

            var expressions = new Expressions(itemExpression, listTypes);

            var currentIndex = 0;
            foreach (Expression expression in expressions)
            {
                var innerExpressions = new Expressions(expression, listTypes);

                foreach (Expression innerExpression in innerExpressions)
                {
                    var newDrawable = Interpreter.GetDrawable(innerExpression);

                    if (newDrawable == null)
                    {
                        return drawables;
                    }

                    newDrawable.Description = "[" + drawables.Count + "]: " + newDrawable.Description;
                    drawables.Add(newDrawable);

                    currentIndex++;
                    if (currentIndex >= MAX_SEGMENTS)
                    {
                        drawables.Error = "Maximum elements per item is currently capped to: " + MAX_SEGMENTS;
                        return drawables;
                    }
                }
            }

            return drawables;
        }
    }
}