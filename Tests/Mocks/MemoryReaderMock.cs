using NeoWatch.Loading;
using System.Collections.Generic;

namespace Tests.Mocks
{
    public class MemoryReaderMock : IMemoryReader
    {
        private readonly Dictionary<ulong, byte[]> memory = new Dictionary<ulong, byte[]>();

        public bool IsAvailable { get; set; } = true;

        public void SetMemory(ulong address, byte[] bytes)
        {
            memory[address] = bytes;
        }

        public bool TryRead(ulong address, byte[] buffer)
        {
            byte[] bytes;
            if (!memory.TryGetValue(address, out bytes)) return false;
            if (bytes.Length < buffer.Length) return false;

            System.Buffer.BlockCopy(bytes, 0, buffer, 0, buffer.Length);
            return true;
        }
    }
}
