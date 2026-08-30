using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoWatch.Drawing;
using NeoWatch.Geometries;
using NeoWatch.Loading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Tests.Mocks;

namespace Tests
{
    [TestClass]
    public class DemoMemoryBlueprintTests
    {
        [DataTestMethod]
        [DataRow("stressPoints", "DemoPoint", 4)]
        [DataRow("stressPoints", "DemoPoint", 8)]
        [DataRow("stressSegments", "DemoLineSegment", 4)]
        [DataRow("stressSegments", "DemoLineSegment", 8)]
        [DataRow("stressArcs", "DemoArcSegment", 4)]
        [DataRow("stressArcs", "DemoArcSegment", 8)]
        public async Task bundled_stress_blueprints_decode_all_elements_in_one_read(
            string name, string elementType, int pointerSize)
        {
            string settings;
            using (var stream = typeof(DemoMemoryBlueprintTests).Assembly
                .GetManifestResourceStream("DemoBlueprintSettings.json"))
            using (var reader = new StreamReader(stream)) settings = reader.ReadToEnd();
            var manifest = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(settings);
            var properties = (Dictionary<string, object>)manifest["properties"];
            var setting = (Dictionary<string, object>)properties["neoWatch.general.linkedListMemoryBlueprints"];
            string blueprints = (string)setting["default"];
            string type = "std::vector<" + elementType + ",std::allocator<" + elementType + "> >";
            Assert.AreEqual(1, LinkedListMemoryBlueprintParser.Parse(blueprints).Count(b => b.Matches(type)));

            // Follow the actual C++ declaration order, independently of the INI field order.
            string[] fields = elementType == "DemoPoint" ? new[] { "demoX", "demoY" }
                : elementType == "DemoLineSegment" ? new[] { "demoInitialPoint.demoX", "demoInitialPoint.demoY",
                    "demoFinalPoint.demoX", "demoFinalPoint.demoY" }
                : new[] { "demoCenterPoint.demoX", "demoCenterPoint.demoY", "demoInitialAngle",
                    "demoSweepAngle", "demoRadius" };
            int stride = fields.Length * sizeof(double);
            const int count = 2000;
            const ulong head = 0x1000;
            string prefix = "(" + name + ")._Mypair._Myval2.";
            var debugger = new DebuggerMock { CurrentProcessId = 42 };
            debugger.GetExpressionWithOptionsCallback = (expression, expand) =>
            {
                Assert.IsFalse(expand);
                if (expression == name) return Value(string.Empty, type);
                if (expression == prefix + "_Myfirst") return Value("0x1000", elementType + " *");
                if (expression == prefix + "_Mylast" || expression == prefix + "_Myend")
                    return Value("0x" + (head + (ulong)(count * stride)).ToString("x"), elementType + " *");
                if (expression == "sizeof(*(" + prefix + "_Myfirst))") return Value(stride.ToString());
                if (expression == "sizeof(void*)") return Value(pointerSize.ToString());
                for (int field = 0; field < fields.Length; field++)
                    if (expression.Contains("->" + fields[field] + ")"))
                        return expression.StartsWith("(long long)", StringComparison.Ordinal)
                            ? Value((field * sizeof(double)).ToString()) : Value("1", "double");
                Assert.Fail("Unexpected expression: " + expression);
                return null;
            };
            int natvisCalls = 0;
            debugger.GetExpressionCallback = expression => { natvisCalls++; return null; };
            var memory = new MemoryReaderMock();
            var values = new double[count * fields.Length];
            for (int i = 0; i < count; i++)
                for (int field = 0; field < fields.Length; field++)
                    values[i * fields.Length + field] = i + field + 0.25;
            var bytes = new byte[count * stride];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            memory.SetMemory(head, bytes);
            var loader = new Loader(debugger, new InterpreterMock(), memory);
            loader.ConfigureLinkedListMemoryLoading(true, blueprints);
            var item = new WatchItem { Name = name };

            var result = await loader.Load(item);
            Assert.IsFalse(result.Feedback.HasError);
            Assert.AreEqual(count, result.Data.Count);
            Assert.AreEqual(0, natvisCalls);
            Assert.AreEqual(1, memory.ReadCount);
            Assert.AreEqual(type, item.Snapshot.ContiguousBlueprintType);
            for (int i = 0; i < count; i++)
            {
                var first = new Point(i + 0.25f, i + 1.25f);
                IDrawable expected = elementType == "DemoPoint" ? (IDrawable)new DrawablePoint(first.X, first.Y)
                    : elementType == "DemoLineSegment" ? new DrawableLineSegment(first, new Point(i + 2.25f, i + 3.25f))
                    : (IDrawable)new DrawableArcSegment(first, i + 2.25f, i + 3.25f, i + 4.25f);
                Assert.AreEqual(expected, result.Data[i], "Wrong geometry at " + i);
            }
            Assert.AreEqual(ReloadScope.Nothing, loader.PlanReload(item).Scope);
            Buffer.BlockCopy(BitConverter.GetBytes(-99.0), 0, bytes, (count - 1) * stride, sizeof(double));
            memory.SetMemory(head, bytes);
            CollectionAssert.AreEqual(new[] { count - 1 }, loader.PlanReload(item).ChangedIndices);
            Assert.AreEqual(0, natvisCalls);
        }

        private static ExpressionMock Value(string value, string type = "int")
        {
            return new ExpressionMock(value, type, () => string.Empty);
        }
    }
}
