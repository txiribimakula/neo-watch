using NeoWatch.Loading;
using System.Runtime.InteropServices;

namespace Tests.Mocks
{
    public class DebuggerMock : IDebugger
    {
        public delegate IExpression Callback(string name);
        public delegate IExpression CallbackWithOptions(string name, bool useAutoExpandRules);

        public Callback GetExpressionCallback { get; set; } = (name) => new ExpressionMock(name, "any", () => throw new COMException());
        public CallbackWithOptions GetExpressionWithOptionsCallback { get; set; }

        public IExpression GetExpression(string name)
        {
            return GetExpressionCallback(name);
        }

        public IExpression GetExpression(string name, bool useAutoExpandRules)
        {
            if (GetExpressionWithOptionsCallback != null)
            {
                return GetExpressionWithOptionsCallback(name, useAutoExpandRules);
            }
            return GetExpressionCallback(name);
        }

        /// <summary>Zero means "nothing being debugged", which keeps the memory reader out.</summary>
        public int CurrentProcessId { get; set; }
    }
}
