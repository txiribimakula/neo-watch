using DTE = EnvDTE;

namespace NeoWatch.Loading
{
    public class Debugger : IDebugger
    {
        DTE::Debugger _debugger;

        public Debugger(DTE::Debugger debugger)
        {
            _debugger = debugger;
        }

        public IExpression GetExpression(string name)
        {
            var expression = _debugger.GetExpression(name, true);

            return new Expression(expression);
        }

        public int CurrentProcessId
        {
            get
            {
                try
                {
                    var process = _debugger.CurrentProcess;
                    return process == null ? 0 : process.ProcessID;
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    return 0;
                }
            }
        }
    }
}
