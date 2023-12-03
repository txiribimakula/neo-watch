using NeoWatch.Common;
using NeoWatch.Drawing;
using NeoWatch.Loading;
using System;
using System.Collections.Generic;

namespace Tests.Mocks
{
    public class InterpreterMock : IInterpreter
    {
        public Dictionary<PatternKind, string[]> Patterns { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Dictionary<string, PatternKind> TypeKindPairs { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public Result<IDrawable> GetDrawable(IExpression expression)
        {
            throw new NotImplementedException();
        }
    }
}
