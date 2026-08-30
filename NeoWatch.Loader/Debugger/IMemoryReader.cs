namespace NeoWatch.Loading
{
    /// <summary>
    /// Reads raw memory from the process being debugged.
    ///
    /// Deliberately tiny and failure-tolerant: the only implementation talks to the debugger
    /// through an API that is not officially supported from the IDE side, so every call must be
    /// allowed to come back false and send the caller down the normal DTE path.
    /// </summary>
    public interface IMemoryReader
    {
        /// <summary>True while the reader has a usable connection to the debuggee.</summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Fills <paramref name="buffer"/> from <paramref name="address"/>. Returns false on any
        /// failure — bad address, no process, unsupported API — without throwing.
        /// </summary>
        bool TryRead(ulong address, byte[] buffer);

        /// <summary>Forgets all process-specific state when a debug session ends.</summary>
        void Reset();
    }
}
