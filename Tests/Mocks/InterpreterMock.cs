using NeoWatch.Common;
using NeoWatch.Drawing;
using NeoWatch.Loading;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Tests.Mocks
{
    public class InterpreterMock : IInterpreter
    {
        public Dictionary<PatternKind, string[]> Patterns { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Dictionary<string, PatternKind> TypeKindPairs { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }


        public delegate Result<IDrawable> Callback(IExpression expression);
        public Callback GetDrawableCallback { get; set; } = (expression) => new Result<IDrawable>(new Drawable("description"));
        public Result<IDrawable> GetDrawable(IExpression expression)
        {
            return GetDrawableCallback(expression);
        }
    }
}
