using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using NeoWatch.Drawing;

namespace NeoWatch.Benchmark
{
    /// <summary>
    /// Measures the load phase — the part where the progress bar climbs — minus the COM
    /// round-trips, which cannot be reproduced outside a live debugging session.
    ///
    /// Everything measured here is per-element work the extension does on top of COM, so it
    /// is the part that can be attacked without touching the debugger integration at all.
    ///
    /// The patterns and the parsing steps mirror BlueprintsOptionPage, Matcher.GetMatch and
    /// PointInterpreter.ToDrawable.
    /// </summary>
    internal static class LoadPhase
    {
        private const string TypePattern = @"(?<type>\w+): (?<parse>.*)";

        private static readonly string[] PointPatterns =
        {
            @"^\((?<x>\d*\.?\d+),(?<y>\d*\.?\d+)\)$",
            @"\((?<x>.*),(?<y>.*)\)"
        };

        public static void Run(int[] sizes)
        {
            Console.WriteLine();
            Console.WriteLine("Fase de carga - todo menos las llamadas COM, que no se pueden reproducir aqui");
            Console.WriteLine();
            Console.WriteLine("  +---------+------------+------------+------------+------------+");
            Console.WriteLine("  |  Puntos | Rgx estat. | Rgx compil.| Parse+alloc| Yields+bar |");
            Console.WriteLine("  +---------+------------+------------+------------+------------+");

            foreach (int size in sizes)
            {
                List<string> values = BuildValues(size);

                Console.WriteLine("  | " + size.ToString("N0", CultureInfo.InvariantCulture).PadLeft(7)
                                  + " | " + Ms(TimeRegexStatic(values)).PadLeft(10)
                                  + " | " + Ms(TimeRegexCompiled(values)).PadLeft(10)
                                  + " | " + Ms(TimeParseAndAlloc(values)).PadLeft(10)
                                  + " | " + Ms(TimeYields(size)).PadLeft(10) + " |");
            }

            Console.WriteLine("  +---------+------------+------------+------------+------------+");
            Console.WriteLine();
            Console.WriteLine("  Rgx estat.   los Regex.Match estaticos de Matcher.GetMatch, 2-3 por elemento");
            Console.WriteLine("  Rgx compil.  los mismos, con instancias Regex reutilizadas y RegexOptions.Compiled");
            Console.WriteLine("  Parse+alloc  float.TryParse x2 y new DrawablePoint, lo que hace PointInterpreter");
            Console.WriteLine("  Yields+bar   los N/100 Dispatcher.Yield de Loader.cs:97 con su actualizacion de barra");
            Console.WriteLine();
            Console.WriteLine("  NO incluido: las ~4 llamadas COM por elemento contra el evaluador del depurador,");
            Console.WriteLine("  que necesitan una sesion de depuracion viva. Ese es el resto del tiempo real.");
            Console.WriteLine();
        }

        private static string Ms(double milliseconds)
        {
            return milliseconds >= 1000
                ? (milliseconds / 1000.0).ToString("F2", CultureInfo.InvariantCulture) + " s"
                : milliseconds.ToString("F0", CultureInfo.InvariantCulture) + " ms";
        }

        /// <summary>Values shaped exactly like the demo NatVis DisplayString for DemoPoint.</summary>
        private static List<string> BuildValues(int count)
        {
            var list = new List<string>(count);
            var rnd = new Random(1234);

            for (int i = 0; i < count; i++)
            {
                double x = rnd.NextDouble() * 20.0;
                double y = rnd.NextDouble() * 20.0;
                list.Add("Pnt: (" + x.ToString("F2", CultureInfo.InvariantCulture)
                         + "," + y.ToString("F2", CultureInfo.InvariantCulture) + ")");
            }

            return list;
        }

        /// <summary>What Matcher.GetMatch does today: the static Regex.Match overload.</summary>
        private static double TimeRegexStatic(List<string> values)
        {
            Regex.Match(values[0], TypePattern);

            var sw = Stopwatch.StartNew();
            foreach (string value in values)
            {
                Match typeMatch = Regex.Match(value, TypePattern);
                string parse = typeMatch.Groups["parse"].Value;
                foreach (string pattern in PointPatterns)
                {
                    if (Regex.Match(parse, pattern).Success) break;
                }
            }
            return sw.Elapsed.TotalMilliseconds;
        }

        /// <summary>The same matches against reused, pre-compiled Regex instances.</summary>
        private static double TimeRegexCompiled(List<string> values)
        {
            var typeRegex = new Regex(TypePattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
            var pointRegexes = new Regex[PointPatterns.Length];
            for (int i = 0; i < pointRegexes.Length; i++)
            {
                pointRegexes[i] = new Regex(PointPatterns[i], RegexOptions.Compiled | RegexOptions.CultureInvariant);
            }

            typeRegex.Match(values[0]);

            var sw = Stopwatch.StartNew();
            foreach (string value in values)
            {
                Match typeMatch = typeRegex.Match(value);
                string parse = typeMatch.Groups["parse"].Value;
                foreach (Regex regex in pointRegexes)
                {
                    if (regex.Match(parse).Success) break;
                }
            }
            return sw.Elapsed.TotalMilliseconds;
        }

        /// <summary>PointInterpreter.ToDrawable minus the regex: two TryParse and one allocation.</summary>
        private static double TimeParseAndAlloc(List<string> values)
        {
            var pointRegex = new Regex(PointPatterns[1], RegexOptions.Compiled | RegexOptions.CultureInvariant);
            var matches = new Match[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                matches[i] = pointRegex.Match(values[i]);
            }

            var sw = Stopwatch.StartNew();
            foreach (Match match in matches)
            {
                float x, y;
                float.TryParse(match.Groups["x"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                float.TryParse(match.Groups["y"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                GC.KeepAlive(new DrawablePoint(x, y));
            }
            return sw.Elapsed.TotalMilliseconds;
        }

        /// <summary>Loader.cs:97 — one yield every 100 elements, each updating the progress bar.</summary>
        private static double TimeYields(int count)
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            int loadingCount = 0;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                if (i % 100 == 0)
                {
                    loadingCount = i;
                    dispatcher.Invoke(new Action(delegate { }), DispatcherPriority.Background);
                }
            }
            GC.KeepAlive(loadingCount);
            return sw.Elapsed.TotalMilliseconds;
        }
    }
}
