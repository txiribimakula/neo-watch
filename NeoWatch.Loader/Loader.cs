using EnvDTE;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NeoWatch.Drawing;

namespace NeoWatch.Loading
{
    public class Loader
    {
        private readonly int MAX_SEGMENTS = 400;

        private KnownColor[] colors = {
            KnownColor.Black,
            KnownColor.DarkRed,
            KnownColor.DarkGreen,
            KnownColor.Yellow,
            KnownColor.DarkOrange,
            KnownColor.DarkBlue,
            KnownColor.DeepPink,
            KnownColor.HotPink,
            KnownColor.Brown,
        };

        private int currentColorIndex = -1;

        private string nextColor()
        {
            currentColorIndex++;
            if(currentColorIndex == colors.Length)
            {
                currentColorIndex = 0;
            }
            return "#" + (System.Drawing.Color.FromKnownColor(colors[currentColorIndex]).ToArgb() & 0x00FFFFFF).ToString("X6");
        }

        private Debugger debugger;

        public Interpreter Interpreter { get; set; }

        public Loader(Debugger debugger, Interpreter interpreter)
        {
            this.debugger = debugger;
            Interpreter = interpreter;
        }

        public Task<Drawables> Load(WatchItem item) {
            item.Drawables.Progress = 0;

            Expression expression = null;
            try
            {
                expression = debugger.GetExpression(item.Name, true);
            }
            catch (COMException)
            {
                return Task.FromResult<Drawables>(null);
            }

            if (expression == null || string.IsNullOrEmpty(expression.Type))
            {
                item.Drawables.Progress = 0;
                item.Drawables.Error = "Variable could not be found.";
                return Task.FromResult<Drawables>(null);
            }

            item.Description = expression.Type;
            if(item.Color == null)
            {
                item.Color = nextColor();
            }

            return GetDrawablesAsync(expression);
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

            var expressions = GetInterpreters(itemExpression);

            var currentIndex = 0;
            foreach (Expression expression in expressions)
            {
                var expressionValue = expression.Value;
                var newDrawable = Interpreter.GetDrawable(expressionValue);

                if(newDrawable == null)
                {
                    try
                    {
                        newDrawable = Interpreter.GetDrawable(expression.DataMembers.Item("Parse").Value);
                    }
                    catch (COMException)
                    {
                        return drawables;
                    }
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

            return drawables;
        }

        private IEnumerable<Expression> GetInterpreters(Expression expression)
        {
            var expressionType = expression.Type;
            
            if (expressionType.StartsWith("std::vector") || expressionType.StartsWith("System.Collections.Generic.List"))
            {
                return new Expressions(expression);
            }

            var expressionValue = expression.Value;
            if (expressionValue.Contains("List"))
            {
                return new Expressions(expression);
            }

            return new List<Expression>()
            {
                expression
            };
        }
    }
}