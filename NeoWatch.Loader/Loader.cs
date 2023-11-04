﻿using EnvDTE;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NeoWatch.Drawing;

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
                item.Color = Colours.NextColor().AsHex();
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

            var expressions = new Expressions(itemExpression, new string[]
            {
                "std::vector",
                "std::array",
                "System.Collections.Generic.List"
            });

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
    }
}