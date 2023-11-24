using NeoWatch.Loading;
using System.Runtime.InteropServices;

namespace Tests.Mocks
{
    public class DebuggerMock : IDebugger
    {
        public delegate IExpression Callback(string name);

        public Callback GetExpressionCallback { get; set; } = (name) => new ExpressionMock(name, () => throw new COMException());

        public IExpression GetExpression(string name)
        {
            return GetExpressionCallback(name);
        }
    }
}
