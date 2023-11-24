using NeoWatch.Drawing;
using NeoWatch.Loading;

namespace Tests.Mocks
{
    public class InterpreterMock : IInterpreter
    {
        public Dictionary<PatternKind, string[]> Patterns { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Dictionary<string, PatternKind> TypeKindPairs { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public IDrawable GetDrawable(IExpression expression)
        {
            throw new NotImplementedException();
        }
    }
}
