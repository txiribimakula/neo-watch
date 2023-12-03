﻿using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NeoWatch.Drawing;
using NeoWatch.Common;

namespace NeoWatch.Loading
{
    public class Loader
    {
        private readonly int MAX_SEGMENTS = 400;

        private IDebugger debugger;

        public IInterpreter Interpreter { get; set; }

        public Loader(IDebugger debugger, IInterpreter interpreter)
        {
            this.debugger = debugger;
            Interpreter = interpreter;
        }

        public async Task<Result<Drawables>> Load(WatchItem item) {

            IExpression expression = null;

            try
            {
                expression = debugger.GetExpression(item.Name);
            }
            catch (COMException)
            {
                return new Result<Drawables>(FeedbackType.ExpressionLoadException);
            }

            if (expression == null || string.IsNullOrEmpty(expression.Type))
            {
                return new Result<Drawables>(FeedbackType.ExpressionLoadException);
            }

            var drawablesResult = await GetDrawablesAsync(expression);
            if(drawablesResult.Data != null)
            {
                drawablesResult.Data.Type = expression.Type;
            }

            return drawablesResult;
        }

        private Task<Result<Drawables>> GetDrawablesAsync(IExpression expression)
        {
            return Task.Run(() => {
                return GetDrawables(expression);
            });
        }

        private Result<Drawables> GetDrawables(IExpression itemExpression)
        {
            var drawables = new Drawables();

            var listTypes = new string[]
            {
                "std::vector",
                "std::array",
                "System.Collections.Generic.List"
            };

            var expressions = new ExpressionLoader(itemExpression, listTypes);

            var currentIndex = 0;
            foreach (IExpression expression in expressions)
            {
                var innerExpressions = new ExpressionLoader(expression, listTypes);

                foreach (IExpression innerExpression in innerExpressions)
                {
                    var newDrawableResult = Interpreter.GetDrawable(innerExpression);

                    if (newDrawableResult.Feedback.Type != FeedbackType.OK)
                    {
                        return new Result<Drawables>(drawables, newDrawableResult.Feedback);
                    }

                    newDrawableResult.Data.Description = "[" + drawables.Count + "]: " + newDrawableResult.Data.Description;
                    drawables.Add(newDrawableResult.Data);

                    currentIndex++;
                    if (currentIndex >= MAX_SEGMENTS)
                    {
                        return new Result<Drawables>(FeedbackType.MaximumElementsCap);
                    }
                }
            }

            return new Result<Drawables>(drawables);
        }
    }
}