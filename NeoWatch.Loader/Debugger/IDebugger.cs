namespace NeoWatch.Loading
{
    public interface IDebugger
    {
        IExpression GetExpression(string name);

        IExpression GetExpression(string name, bool useAutoExpandRules);

        /// <summary>
        /// Native process id of the debuggee, or 0 when nothing is being debugged. Used to pick
        /// the right process out of the ones the memory reader can see.
        /// </summary>
        int CurrentProcessId { get; }
    }
}
