using System;
using Microsoft.VisualStudio.Debugger;
using NeoWatch.Loading;

namespace NeoWatch.Debugging
{
    /// <summary>
    /// Reads the debuggee's memory through Concord.
    ///
    /// Calling these APIs from the IDE side rather than from a debugger component is not
    /// officially supported, so every entry point swallows failures and reports unavailability.
    /// When that happens the loader simply keeps doing what it did before: nothing degrades
    /// beyond losing the shortcut.
    /// </summary>
    public class DkmMemoryReader : IMemoryReader
    {
        private readonly IDebugger debugger;
        private bool disabled;
        private int cachedProcessId;
        private DkmProcess cachedProcess;

        public DkmMemoryReader(IDebugger debugger)
        {
            this.debugger = debugger;
        }

        /// <summary>Set once a call has thrown, so the failure is paid at most once per session.</summary>
        public bool IsAvailable
        {
            get { return !disabled; }
        }

        public void Reset()
        {
            cachedProcess = null;
            cachedProcessId = 0;
            disabled = false;
        }

        public bool TryRead(ulong address, byte[] buffer)
        {
            if (disabled || address == 0 || buffer == null || buffer.Length == 0) return false;

            try
            {
                DkmProcess process = FindProcess();
                if (process == null) return false;

                int bytesRead = process.ReadMemory(address, DkmReadMemoryFlags.None, buffer);
                return bytesRead == buffer.Length;
            }
            catch (DkmException)
            {
                // Bad address or a process that moved on: expected, and not a reason to give up
                // on the feature as a whole.
                cachedProcess = null;
                cachedProcessId = 0;
                return false;
            }
            catch (Exception)
            {
                // Anything else means the API is not usable from here at all.
                disabled = true;
                return false;
            }
        }

        private DkmProcess FindProcess()
        {
            int processId = debugger.CurrentProcessId;
            if (processId == 0)
            {
                cachedProcess = null;
                cachedProcessId = 0;
                return null;
            }

            // DkmProcess.GetProcesses crosses into Concord. A linked list performs one memory
            // read per node, so resolving the same process for every node dwarfs the reads.
            if (cachedProcess != null && cachedProcessId == processId) return cachedProcess;

            DkmProcess[] processes = DkmProcess.GetProcesses();
            if (processes == null) return null;

            foreach (DkmProcess process in processes)
            {
                if (process.LivePart != null && process.LivePart.Id == processId)
                {
                    cachedProcess = process;
                    cachedProcessId = processId;
                    return process;
                }
            }

            return null;
        }
    }
}
