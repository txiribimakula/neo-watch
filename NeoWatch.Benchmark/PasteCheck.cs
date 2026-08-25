using System;
using System.Collections.Generic;
using NeoWatch.Loading;

namespace NeoWatch.Benchmark
{
    /// <summary>
    /// Checks the parser that turns pasted text into watch expressions, against the shape Visual
    /// Studio puts on the clipboard when you copy rows out of the Watch window.
    /// </summary>
    internal static class PasteCheck
    {
        public static int Run()
        {
            Console.WriteLine();
            Console.WriteLine("Comprobacion del parser de pegado");
            Console.WriteLine();

            bool ok = true;

            ok &= Case("una fila del Watch",
                       "checkPoints\t{ size=40 }\tstd::vector<DemoPoint,std::allocator<DemoPoint> >",
                       "checkPoints");

            ok &= Case("varias filas",
                       "checkPoints\t{ size=40 }\tstd::vector<DemoPoint>\r\n"
                       + "checkArcs\t{ size=12 }\tstd::vector<DemoArcSegment>",
                       "checkPoints", "checkArcs");

            ok &= Case("texto plano sin tabuladores", "f10Points\r\nchainNodes[0]", "f10Points", "chainNodes[0]");
            ok &= Case("expresion indexada", "checkMixed[7]\tArc: C: ...\tDemoSegment", "checkMixed[7]");
            ok &= Case("hijo expandido, viene indentado", "\t[0]\tPnt: (1,2)\tDemoPoint", "[0]");
            ok &= Case("lineas vacias en medio", "a\r\n\r\n\r\nb", "a", "b");
            ok &= Case("duplicados descartados", "a\r\nb\r\na", "a", "b");
            // El expansor de la fila viaja con ella al copiar desde el Watch.
            ok &= Case("solo el expansor", "+");
            ok &= Case("expansor en columna propia", "+\tcheckPoints\t{ size=40 }\tstd::vector", "checkPoints");
            ok &= Case("expansor pegado al nombre", "- checkArcs\t{ size=12 }", "checkArcs");
            ok &= Case("expansor de flecha", "▶ f10Points\t{ size=3000 }", "f10Points");
            ok &= Case("expansores y filas reales mezclados", "+\r\ncheckPoints\t{ size=40 }\r\n-", "checkPoints");

            // Copiar sin seleccion en el editor devuelve la linea entera de codigo.
            ok &= Case("linea de codigo completa",
                       "    auto checkMixed = MakeCheckMixed();         // 24 elementos: lineas y arcos alternados");
            ok &= Case("sentencia suelta", "checkMixed.push_back(extraSegment);");
            ok &= Case("comentario de bloque", "checkPoints /* ojo */");
            ok &= Case("codigo y una expresion buena", "int i = 0;\r\ncheckPoints", "checkPoints");

            ok &= Case("solo espacios", "   \r\n\t\r\n");
            ok &= Case("nulo", null);
            ok &= Case("vacio", "");

            // El tope existe para que pegar un fichero entero por error no tumbe la ventana.
            var many = new List<string>();
            for (int i = 0; i < WatchExpressionParser.MaxExpressions + 50; i++) many.Add("v" + i);
            List<string> capped = WatchExpressionParser.Parse(string.Join("\r\n", many.ToArray()));
            bool cappedOk = capped.Count == WatchExpressionParser.MaxExpressions;
            Report("tope de " + WatchExpressionParser.MaxExpressions, cappedOk,
                   capped.Count.ToString(), WatchExpressionParser.MaxExpressions.ToString());
            ok &= cappedOk;

            Console.WriteLine();
            Console.WriteLine(ok ? "  Todo correcto." : "  HAY FALLOS.");
            Console.WriteLine();
            return ok ? 0 : 1;
        }

        private static bool Case(string label, string text, params string[] expected)
        {
            List<string> got = WatchExpressionParser.Parse(text);

            bool ok = got.Count == expected.Length;
            if (ok)
            {
                for (int i = 0; i < expected.Length; i++)
                {
                    if (got[i] != expected[i]) { ok = false; break; }
                }
            }

            Report(label, ok, string.Join("|", got.ToArray()), string.Join("|", expected));
            return ok;
        }

        private static void Report(string label, bool ok, string got, string expected)
        {
            Console.WriteLine("  " + (ok ? "ok   " : "FALLO") + "  " + label.PadRight(34)
                              + (ok ? string.Empty : "esperado [" + expected + "], obtenido [" + got + "]"));
        }
    }
}
