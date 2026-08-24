using System;
using System.Collections.Generic;
using NeoWatch.Loading;

namespace NeoWatch.Benchmark
{
    /// <summary>
    /// Exercises the diff that C0b and C0c stand on, against synthetic blocks.
    ///
    /// This is the part whose mistakes would be silent — drawing stale data rather than crashing —
    /// and the unit test project cannot run on this machine, so it gets checked here instead.
    /// </summary>
    internal static class SnapshotCheck
    {
        private const int Stride = 16;
        private const int Count = 100;

        public static int Run()
        {
            Console.WriteLine();
            Console.WriteLine("Comprobacion del diff de instantaneas");
            Console.WriteLine();

            bool ok = true;

            ok &= Case("sin cambios", Build(Count), Count, 8, new int[0]);
            ok &= Case("uno cambiado, el 50", Mutate(Build(Count), 50), Count, 8, new[] { 50 });
            ok &= Case("el primero", Mutate(Build(Count), 0), Count, 8, new[] { 0 });
            ok &= Case("el ultimo", Mutate(Build(Count), Count - 1), Count, 8, new[] { Count - 1 });
            ok &= Case("dos separados", Mutate(Mutate(Build(Count), 3), 77), Count, 8, new[] { 3, 77 });
            ok &= Case("cambio en el ultimo byte del elemento", MutateByte(Build(Count), 9, Stride - 1), Count, 8, new[] { 9 });

            // Crecimiento: el prefijo intacto, la cola es nueva y no sale en el diff.
            ok &= Case("crece en 1, prefijo intacto", Build(Count + 1), Count + 1, 8, new int[0]);
            ok &= Case("crece en 1 y ademas cambia uno", Mutate(Build(Count + 1), 20), Count + 1, 8, new[] { 20 });

            // Truncado: se compara solo lo que sobrevive.
            ok &= Case("trunca a 60", Build(60), 60, 8, new int[0]);
            ok &= Case("trunca a 60 y cambia el 10", Mutate(Build(60), 10), 60, 8, new[] { 10 });

            // Por encima del limite, null: el que llama recarga entero.
            ok &= CaseNull("mas cambios que el limite", MutateMany(Build(Count), 20), Count, 8);

            // Sin correspondencia uno a uno, null pase lo que pase.
            ok &= CaseNull("sin mapeo por elemento", Build(Count), Count, 8, supportsPartial: false);

            Console.WriteLine();
            Console.WriteLine(ok ? "  Todo correcto." : "  HAY FALLOS.");
            Console.WriteLine();
            return ok ? 0 : 1;
        }

        private static MemorySnapshot Baseline(bool supportsPartial = true)
        {
            return new MemorySnapshot(0x1000, "{ size=100 }", Stride, Count, supportsPartial, Build(Count));
        }

        /// <summary>Deterministic bytes, so every element differs from its neighbours.</summary>
        private static byte[] Build(int count)
        {
            var bytes = new byte[count * Stride];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)((i * 31 + 7) % 251);
            }
            return bytes;
        }

        private static byte[] Mutate(byte[] bytes, int index)
        {
            return MutateByte(bytes, index, 0);
        }

        private static byte[] MutateByte(byte[] bytes, int index, int offset)
        {
            bytes[index * Stride + offset] ^= 0xFF;
            return bytes;
        }

        private static byte[] MutateMany(byte[] bytes, int howMany)
        {
            for (int i = 0; i < howMany; i++)
            {
                Mutate(bytes, i * 3);
            }
            return bytes;
        }

        private static bool Case(string label, byte[] other, int otherCount, int limit, int[] expected)
        {
            List<int> changed = Baseline().FindChangedElements(other, otherCount, limit);

            bool ok = changed != null && changed.Count == expected.Length;
            if (ok)
            {
                for (int i = 0; i < expected.Length; i++)
                {
                    if (changed[i] != expected[i]) { ok = false; break; }
                }
            }

            Report(label, ok, changed == null ? "null" : string.Join(",", changed.ConvertAll(i => i.ToString()).ToArray()),
                   string.Join(",", Array.ConvertAll(expected, i => i.ToString())));
            return ok;
        }

        private static bool CaseNull(string label, byte[] other, int otherCount, int limit, bool supportsPartial = true)
        {
            List<int> changed = Baseline(supportsPartial).FindChangedElements(other, otherCount, limit);
            bool ok = changed == null;
            Report(label, ok, changed == null ? "null" : changed.Count + " indices", "null");
            return ok;
        }

        private static void Report(string label, bool ok, string got, string expected)
        {
            Console.WriteLine("  " + (ok ? "ok   " : "FALLO") + "  " + label.PadRight(38)
                              + (ok ? string.Empty : "esperado [" + expected + "], obtenido [" + got + "]"));
        }
    }
}
