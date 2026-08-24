using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace NeoWatch.Benchmark
{
    /// <summary>
    /// Finds where the targeted reload of C0b stops being worth it and a full sweep wins.
    ///
    /// The two paths do a different number of debugger round-trips per element, and that is the
    /// only thing that matters — everything else is noise beside a COM call:
    ///
    ///   full sweep      3 reads per element, for every element
    ///                   plus the fixed cost of resetting and refilling the collection
    ///   targeted        2 fixed reads to plan, plus GetExpression("v[i]") per changed element,
    ///                   which costs more than one step of an existing enumerator because it
    ///                   re-evaluates the container and the index
    ///
    /// The per-call figures cannot be measured outside a live debugging session, so this sweeps a
    /// range of plausible values and reports the crossover for each. Feed it the real numbers once
    /// they are known: load a container in the extension, read the elapsed time off the Status
    /// column, and divide by elements times three.
    /// </summary>
    internal static class ThresholdProbe
    {
        /// <summary>Microseconds for one property read during a full sweep.</summary>
        private static readonly double[] SweepReadCosts = { 50, 100, 200, 400 };

        /// <summary>
        /// How much dearer a standalone GetExpression("v[i]") is than one step of the sweep.
        /// A standalone evaluation parses the expression and walks the container again.
        /// </summary>
        private static readonly double[] TargetedPenalties = { 2.0, 4.0, 8.0 };

        private const int ReadsPerElement = 3;

        public static void Run(int[] sizes)
        {
            Console.WriteLine();
            Console.WriteLine("Umbral de C0b: a partir de que fraccion cambiada conviene recargar entero");
            Console.WriteLine();
            Console.WriteLine("  Modelo:  barrido   = n * 3 * coste");
            Console.WriteLine("           puntual   = 2 * coste + cambiados * 3 * coste * penalizacion");
            Console.WriteLine("  Cruce cuando ambos cuestan lo mismo. La fraccion no depende de n.");
            Console.WriteLine();
            Console.WriteLine("  +----------------+------------------+------------------+");
            Console.WriteLine("  | Coste lectura  | Penalizacion     | Cruce            |");
            Console.WriteLine("  +----------------+------------------+------------------+");

            foreach (double readCost in SweepReadCosts)
            {
                foreach (double penalty in TargetedPenalties)
                {
                    double crossover = Crossover(penalty);
                    Console.WriteLine("  | " + (readCost.ToString("F0", CultureInfo.InvariantCulture) + " us").PadLeft(14)
                                      + " | " + ("x" + penalty.ToString("F1", CultureInfo.InvariantCulture)).PadLeft(16)
                                      + " | " + ((crossover * 100).ToString("F1", CultureInfo.InvariantCulture) + " %").PadLeft(16)
                                      + " |");
                }
            }

            Console.WriteLine("  +----------------+------------------+------------------+");
            Console.WriteLine();
            Console.WriteLine("  El coste por lectura se cancela: el cruce es 1/penalizacion.");
            Console.WriteLine("  Por eso el umbral se puede fijar sin saber cuanto cuesta una llamada COM,");
            Console.WriteLine("  pero SI hace falta saber cuanto mas cara es una evaluacion suelta.");
            Console.WriteLine();

            ReportAbsolute(sizes);
        }

        /// <summary>
        /// Fraction of changed elements at which both paths cost the same. The per-read cost
        /// cancels out, so only the penalty of a standalone evaluation decides it.
        /// </summary>
        private static double Crossover(double penalty)
        {
            // n*3*c  ==  2*c + f*n*3*c*penalty   ->   f ~= 1/penalty for any usable n
            return 1.0 / penalty;
        }

        private static void ReportAbsolute(int[] sizes)
        {
            Console.WriteLine("Coste estimado por parada, con 100 us por lectura y penalizacion x4");
            Console.WriteLine();
            Console.Write("  " + "Elementos".PadRight(12) + "Barrido".PadLeft(12));
            int[] changedCounts = { 1, 10, 100, 1000 };
            foreach (int changed in changedCounts)
            {
                Console.Write((changed + " camb.").PadLeft(12));
            }
            Console.WriteLine();
            Console.WriteLine("  " + new string('-', 24 + 12 * changedCounts.Length));

            const double readCost = 100;
            const double penalty = 4.0;

            foreach (int size in sizes)
            {
                Console.Write("  " + size.ToString("N0", CultureInfo.InvariantCulture).PadRight(12)
                              + Format(size * ReadsPerElement * readCost).PadLeft(12));

                foreach (int changed in changedCounts)
                {
                    if (changed > size)
                    {
                        Console.Write("-".PadLeft(12));
                        continue;
                    }

                    double targeted = 2 * readCost + changed * ReadsPerElement * readCost * penalty;
                    Console.Write(Format(targeted).PadLeft(12));
                }

                Console.WriteLine();
            }

            Console.WriteLine();
            Console.WriteLine("  Microsegundos. Las celdas por encima del barrido son donde conviene recargar entero.");
            Console.WriteLine();
            Console.WriteLine("  PARA MEDIRLO DE VERDAD, en el ejemplar experimental:");
            Console.WriteLine("   1. Carga f10Points y anota los ms de la columna Status -> coste por lectura");
            Console.WriteLine("      = ms * 1000 / (elementos * 3).");
            Console.WriteLine("   2. Pon Loader.PartialReloadFraction a 1.0 para forzar siempre el camino puntual.");
            Console.WriteLine("   3. F10 sobre las lineas que modifican el vector y anota los ms de cada una:");
            Console.WriteLine("      con 1 elemento cambiado sale la penalizacion, y el umbral es 1/penalizacion.");
            Console.WriteLine();
        }

        private static string Format(double microseconds)
        {
            if (microseconds >= 1000000) return (microseconds / 1000000).ToString("F1", CultureInfo.InvariantCulture) + " s";
            if (microseconds >= 1000) return (microseconds / 1000).ToString("F0", CultureInfo.InvariantCulture) + " ms";
            return microseconds.ToString("F0", CultureInfo.InvariantCulture) + " us";
        }
    }
}
