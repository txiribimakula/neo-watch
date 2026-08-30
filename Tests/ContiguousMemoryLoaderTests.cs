using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoWatch.Common;
using NeoWatch.Drawing;
using NeoWatch.Loading;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Tests.Mocks;

namespace Tests
{
    [TestClass]
    public class ContiguousMemoryLoaderTests
    {
        private const string TypeName = "std::vector<DemoPoint,std::allocator<DemoPoint> >";
        private const string Blueprint = @"[std::vector<DemoPoint,std::allocator<DemoPoint>>]
Storage=Contiguous
Head=first
End=last
Capacity=limit
Point.X=demoX|Float64
Point.Y=demoY|Float64";
        private DebuggerMock debugger;
        private MemoryReaderMock memory;
        private Loader loader;
        private WatchItem item;
        private ulong head, end, capacity;
        private int natvisCalls, yOffset;
        private string fieldType, endType, rootType;

        [TestInitialize]
        public void Setup()
        {
            head = 0x1000;
            end = capacity = head + 48;
            yOffset = 8;
            fieldType = "double";
            endType = "DemoPoint *";
            rootType = TypeName;
            natvisCalls = 0;
            debugger = new DebuggerMock { CurrentProcessId = 42 };
            debugger.GetExpressionWithOptionsCallback = (expression, expand) =>
            {
                Assert.IsFalse(expand, "The contiguous path must not expand NatVis.");
                if (expression == "f10Points") return Value(string.Empty, rootType);
                if (expression == "(f10Points).first") return Value("0x" + head.ToString("x"), "DemoPoint *");
                if (expression == "(f10Points).last") return Value("0x" + end.ToString("x"), endType);
                if (expression == "(f10Points).limit") return Value("0x" + capacity.ToString("x"), "DemoPoint *");
                if (expression == "sizeof(*((f10Points).first))") return Value("16");
                if (expression == "sizeof(void*)") return Value("8");
                if (expression.StartsWith("(long long)", StringComparison.Ordinal))
                {
                    return Value(expression.Contains("demoX") ? "0" : yOffset.ToString());
                }
                if (expression.StartsWith("(((f10Points).first)->", StringComparison.Ordinal)) return Value("1", fieldType);
                throw new COMException(expression);
            };
            debugger.GetExpressionCallback = expression =>
            {
                if (expression != "f10Points") throw new COMException();
                natvisCalls++;
                return new ExpressionMock("{ size=1 }", TypeName, () => "parse", 1);
            };
            memory = new MemoryReaderMock();
            memory.SetMemory(head, Points(1, 2, 3, 4, 5, 6));
            var interpreter = new InterpreterMock();
            interpreter.GetDrawableCallback = expression => new Result<IDrawable>(new DrawablePoint(999, 999));
            loader = new Loader(debugger, interpreter, memory);
            loader.ConfigureLinkedListMemoryLoading(true, Blueprint);
            item = new WatchItem { Name = "f10Points" };
        }

        [TestMethod]
        public async Task loads_every_point_in_one_read_without_natvis()
        {
            var result = await loader.Load(item);
            Assert.IsFalse(result.Feedback.HasError);
            Assert.AreEqual(3, result.Data.Count);
            Assert.AreEqual(new DrawablePoint(3, 4), result.Data[1]);
            Assert.AreEqual(1, memory.ReadCount);
            Assert.AreEqual(0, natvisCalls);
            Assert.AreEqual(TypeName, item.Snapshot.ContiguousBlueprintType);
            Assert.AreEqual(ReloadScope.Nothing, loader.PlanReload(item).Scope);
        }

        [TestMethod]
        public async Task prepares_changed_point_from_the_comparison_block()
        {
            await loader.Load(item);
            memory.SetMemory(head, Points(1, 2, 13, 14, 5, 6));
            int reads = memory.ReadCount;
            var plan = loader.PlanReload(item);
            Assert.IsTrue(plan.IsPartial);
            CollectionAssert.AreEqual(new[] { 1 }, plan.ChangedIndices);
            Assert.AreEqual(reads + 1, memory.ReadCount);
            var replacements = loader.ReloadElements(item, plan.ChangedIndices, plan);
            Assert.AreEqual(reads + 1, memory.ReadCount);
            Assert.AreEqual(0, natvisCalls);
            Assert.AreEqual(1, replacements.Count);
            loader.CommitSnapshot(item, plan);
            Assert.AreEqual(ReloadScope.Nothing, loader.PlanReload(item).Scope);
        }

        [DataTestMethod]
        [DataRow(4)]
        [DataRow(8)]
        public async Task node_storage_reads_float_coordinates_in_vector_order_not_link_order(int pointerSize)
        {
            const string nodeType = "std::vector<DemoListOfItself,std::allocator<DemoListOfItself>>";
            const string prefix = "(chainNodeStorage)._Mypair._Myval2.";
            int stride = 2 * pointerSize + 8;
            debugger.GetExpressionWithOptionsCallback = (expression, expand) =>
            {
                Assert.IsFalse(expand, "Node storage must not expand the nested NatVis lists.");
                if (expression == "chainNodeStorage") return Value(string.Empty, nodeType);
                if (expression == prefix + "_Myfirst") return Value("0x1000", "DemoListOfItself *");
                if (expression == prefix + "_Mylast" || expression == prefix + "_Myend")
                    return Value("0x" + (0x1000 + 3 * stride).ToString("x"), "DemoListOfItself *");
                if (expression.StartsWith("sizeof(*", StringComparison.Ordinal)) return Value(stride.ToString());
                if (expression == "sizeof(void*)") return Value(pointerSize.ToString());
                if (expression.StartsWith("(long long)", StringComparison.Ordinal))
                    return Value((2 * pointerSize + (expression.Contains("->x") ? 0 : 4)).ToString());
                if (expression.Contains("->x") || expression.Contains("->y")) return Value("1", "float");
                Assert.Fail("Unexpected expression: " + expression);
                return null;
            };
            var bytes = new byte[3 * stride];
            for (int i = 0; i < 3; i++)
            {
                // Deliberately self-linked: a contiguous blueprint must ignore Next/Previous.
                Array.Copy(BitConverter.GetBytes((ulong)(0x1000 + i * stride)), 0, bytes, i * stride, pointerSize);
                Array.Copy(BitConverter.GetBytes(i + 0.25f), 0, bytes, i * stride + 2 * pointerSize, 4);
                Array.Copy(BitConverter.GetBytes(i + 0.5f), 0, bytes, i * stride + 2 * pointerSize + 4, 4);
            }
            memory.SetMemory(0x1000, bytes);
            loader.ConfigureLinkedListMemoryLoading(true, @"[std::vector<DemoListOfItself,std::allocator<DemoListOfItself>>]
Storage=Contiguous
Head=_Mypair._Myval2._Myfirst
End=_Mypair._Myval2._Mylast
Capacity=_Mypair._Myval2._Myend
Point.X=x|Float32
Point.Y=y|Float32");
            item.Name = "chainNodeStorage";

            var result = await loader.Load(item);

            Assert.IsFalse(result.Feedback.HasError);
            Assert.AreEqual(3, result.Data.Count);
            for (int i = 0; i < 3; i++) Assert.AreEqual(new DrawablePoint(i + 0.25f, i + 0.5f), result.Data[i]);
            Assert.AreEqual(1, memory.ReadCount);
            Assert.AreEqual(0, natvisCalls);
            Assert.AreEqual(nodeType, item.Snapshot.ContiguousBlueprintType);
        }

        [TestMethod]
        public async Task first_load_decodes_three_thousand_points_without_a_prior_snapshot()
        {
            var coordinates = new double[6000];
            for (int i = 0; i < coordinates.Length; i++) coordinates[i] = i * 0.25;
            end = capacity = head + 3000 * 16;
            memory.SetMemory(head, Points(coordinates));
            Assert.IsNull(item.Snapshot);
            var result = await loader.Load(item);
            Assert.IsFalse(result.Feedback.HasError);
            Assert.AreEqual(3000, result.Data.Count);
            for (int i = 0; i < result.Data.Count; i++)
                Assert.AreEqual(new DrawablePoint((float)coordinates[i * 2], (float)coordinates[i * 2 + 1]), result.Data[i]);
            Assert.AreEqual(1, memory.ReadCount);
            Assert.AreEqual(0, natvisCalls);
        }

        [TestMethod]
        public async Task long_decode_still_yields_and_cancels_without_publishing_a_snapshot()
        {
            const int count = 250000;
            end = capacity = head + count * 16;
            memory.SetMemory(head, new byte[count * 16]);
            using (var cancellation = new CancellationTokenSource())
            {
                loader.YieldAction = () =>
                {
                    Assert.IsTrue(item.LoadingCount > 0 && item.LoadingCount < count);
                    cancellation.Cancel();
                    return Task.CompletedTask;
                };
                await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                    () => loader.Load(item, cancellation.Token));
            }
            Assert.IsNull(item.Snapshot);
            Assert.AreEqual(1, memory.ReadCount);
            Assert.AreEqual(0, natvisCalls);
        }

        [TestMethod]
        public async Task growth_reallocation_and_shrink_rebuild_the_snapshot()
        {
            await loader.Load(item);
            head = 0x2000;
            end = capacity = head + 64;
            memory.SetMemory(head, Points(1, 2, 3, 4, 5, 6, 7, 8));
            Assert.AreEqual(ReloadScope.Everything, loader.PlanReload(item).Scope);
            Assert.AreEqual(4, (await loader.Load(item)).Data.Count);
            end = head + 16;
            Assert.AreEqual(ReloadScope.Everything, loader.PlanReload(item).Scope);
            Assert.AreEqual(1, (await loader.Load(item)).Data.Count);
            end = head;
            Assert.AreEqual(ReloadScope.Everything, loader.PlanReload(item).Scope);
            Assert.AreEqual(0, (await loader.Load(item)).Data.Count);
            Assert.AreEqual(0, natvisCalls);
        }

        [TestMethod]
        public async Task empty_vector_does_not_read_memory_or_element_fields()
        {
            head = end = capacity = 0;
            fieldType = "unsupported";
            var result = await loader.Load(item);
            Assert.IsFalse(result.Feedback.HasError);
            Assert.AreEqual(0, result.Data.Count);
            Assert.AreEqual(0, memory.ReadCount);
            Assert.AreEqual(0, natvisCalls);
            Assert.AreEqual(ReloadScope.Nothing, loader.PlanReload(item).Scope);
        }

        [DataTestMethod]
        [DataRow("reversed")]
        [DataRow("capacity")]
        [DataRow("misaligned")]
        [DataRow("null")]
        [DataRow("oversized")]
        [DataRow("pointer-type")]
        [DataRow("scalar-type")]
        [DataRow("offset")]
        [DataRow("unreadable")]
        [DataRow("nan")]
        public async Task invalid_memory_or_layout_falls_back_to_natvis(string failure)
        {
            switch (failure)
            {
                case "reversed": end = head - 16; break;
                case "capacity": capacity = end - 16; break;
                case "misaligned": end--; break;
                case "null": head = 0; break;
                case "oversized": end = capacity = head + 128 * 1024 * 1024; break;
                case "pointer-type": endType = "int *"; break;
                case "scalar-type": fieldType = "float"; break;
                case "offset": yOffset = 12; break;
                case "unreadable": memory.SetMemory(head, new byte[1]); break;
                case "nan": memory.SetMemory(head, Points(double.NaN, 2, 3, 4, 5, 6)); break;
            }
            var result = await loader.Load(item);
            Assert.IsFalse(result.Feedback.HasError);
            Assert.AreEqual(1, natvisCalls);
            Assert.AreEqual(new DrawablePoint(999, 999), result.Data[0]);
            Assert.IsNull(item.Snapshot);
        }

        [TestMethod]
        public async Task disabling_mode_uses_the_original_natvis_loader()
        {
            await loader.Load(item);
            loader.ConfigureLinkedListMemoryLoading(false, Blueprint);
            Assert.AreEqual(ReloadScope.Everything, loader.PlanReload(item).Scope);
            Assert.AreEqual(new DrawablePoint(999, 999), (await loader.Load(item)).Data[0]);
            Assert.AreEqual(1, natvisCalls);
        }

        [TestMethod]
        public async Task another_session_or_type_cannot_skip_reload()
        {
            await loader.Load(item);
            debugger.CurrentProcessId = 43;
            Assert.AreEqual(ReloadScope.Everything, loader.PlanReload(item).Scope);
            debugger.CurrentProcessId = 42;
            rootType = "OtherType";
            Assert.AreEqual(ReloadScope.Everything, loader.PlanReload(item).Scope);
        }

        [TestMethod]
        public async Task cancellation_never_commits_a_partial_snapshot()
        {
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => loader.Load(item, new CancellationToken(true)));
            Assert.IsNull(item.Snapshot);
            Assert.AreEqual(0, natvisCalls);
        }

        [TestMethod]
        public void incomplete_or_unknown_storage_is_ignored()
        {
            Assert.AreEqual(0, LinkedListMemoryBlueprintParser.Parse(Blueprint.Replace("Capacity=limit", "")).Count);
            Assert.AreEqual(0, LinkedListMemoryBlueprintParser.Parse(Blueprint.Replace("Contiguous", "Unknown")).Count);
            Assert.AreEqual(0, LinkedListMemoryBlueprintParser.Parse(Blueprint.Replace("Float64", "99")).Count);
            Assert.IsTrue(LinkedListMemoryBlueprintParser.Parse(Blueprint)[0].Matches(TypeName));
            Assert.IsFalse(new LinkedListMemoryBlueprint { TypeName = "unsigned int" }.Matches("unsignedint"));
        }

        private static ExpressionMock Value(string text, string type = "int")
        {
            return new ExpressionMock(text, type, () => string.Empty);
        }

        private static byte[] Points(params double[] coordinates)
        {
            var bytes = new byte[coordinates.Length * sizeof(double)];
            Buffer.BlockCopy(coordinates, 0, bytes, 0, bytes.Length);
            return bytes;
        }
    }
}
