using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoWatch.Drawing;
using NeoWatch.Loading;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Tests.Mocks;

namespace Tests
{
    [TestClass]
    public class LoaderUnitTest
    {
        [TestClass]
        public class Load_Item
        {
            private Loader loader;
            private DebuggerMock debuggerMock;
            private InterpreterMock interpreterMock;

            public Load_Item()
            {
                debuggerMock = new DebuggerMock();
                interpreterMock = new InterpreterMock();
                loader = new Loader(debuggerMock, interpreterMock);
            }

            [TestMethod]
            public async Task returns_result_with_could_not_load_message_when_getexpression_throws_comexception()
            {
                // Arrange
                debuggerMock.GetExpressionCallback = new DebuggerMock.Callback((string name) => throw new COMException());

                // Act
                var result = await loader.Load(new WatchItem());

                // Assert
                Assert.AreEqual("Expression could not be loaded.", result.Feedback.Detail);
            }

            [TestMethod]
            public void empty_row_controls_are_disabled_and_unchecked_until_it_has_a_name()
            {
                var item = new WatchItem();

                Assert.IsFalse(item.IsRowConfigured);
                Assert.IsFalse(item.IsVisibleControlChecked);
                Assert.IsFalse(item.IsLoadingControlChecked);
                Assert.IsTrue(item.IsVisible);
                Assert.IsTrue(item.IsLoading);

                item.Name = "points";

                Assert.IsTrue(item.IsRowConfigured);
                Assert.IsTrue(item.IsVisibleControlChecked);
                Assert.IsTrue(item.IsLoadingControlChecked);
            }

            [TestMethod]
            public void row_control_values_follow_name_and_update_the_underlying_options()
            {
                var item = new WatchItem { Name = "points" };
                var changes = new List<string>();
                item.PropertyChanged += (sender, args) => changes.Add(args.PropertyName);

                item.IsVisibleControlChecked = false;
                item.IsLoadingControlChecked = false;
                item.Name = null;

                Assert.IsFalse(item.IsVisible);
                Assert.IsFalse(item.IsLoading);
                Assert.IsFalse(item.IsVisibleControlChecked);
                Assert.IsFalse(item.IsLoadingControlChecked);
                CollectionAssert.Contains(changes, nameof(WatchItem.IsVisibleControlChecked));
                CollectionAssert.Contains(changes, nameof(WatchItem.IsLoadingControlChecked));
                CollectionAssert.Contains(changes, nameof(WatchItem.IsRowConfigured));
            }

            [TestMethod]
            public async Task linked_list_mode_rejects_invalid_root_without_natvis_expansion()
            {
                bool requestedAutoExpansion = true;
                debuggerMock.GetExpressionWithOptionsCallback = (name, useAutoExpandRules) =>
                {
                    requestedAutoExpansion = useAutoExpandRules;
                    return new ExpressionMock(string.Empty, string.Empty, () => string.Empty)
                    {
                        IsValidValue = false
                    };
                };
                loader.ConfigureLinkedListMemoryLoading(true, string.Empty);

                var result = await loader.Load(new WatchItem { Name = "notDeclaredYet" });

                Assert.IsTrue(result.Feedback.HasError);
                Assert.IsFalse(requestedAutoExpansion);
            }

            [TestMethod]
            public async Task reacquires_an_indexable_element_if_natvis_is_incomplete_after_yield()
            {
                bool yielded = false;
                int rootReads = 0;
                loader.YieldAction = () => { yielded = true; return Task.CompletedTask; };
                debuggerMock.GetExpressionCallback = name =>
                {
                    Assert.AreEqual("points", name);
                    bool fresh = ++rootReads > 1;
                    var elements = new List<IExpression>();
                    for (int i = 0; i < 101; i++)
                        elements.Add(new ExpressionMock(fresh ? "fresh" : "old", "DemoPoint", () => ""));
                    return new ExpressionMock("{ size=101 }", "std::vector<DemoPoint>", () => "")
                    {
                        DataMembers = new ExpressionsMock(elements)
                    };
                };
                interpreterMock.GetDrawableCallback = expression => yielded && expression.Value != "fresh"
                    ? new NeoWatch.Common.Result<IDrawable>(new NeoWatch.Common.Feedback(NeoWatch.Common.FeedbackType.ExpressionParsingException))
                    : new NeoWatch.Common.Result<IDrawable>(new DrawablePoint(1, 2));

                var result = await loader.Load(new WatchItem { Name = "points" });

                Assert.IsFalse(result.Feedback.HasError);
                Assert.AreEqual(101, result.Data.Count);
                Assert.AreEqual(2, rootReads);
            }

            [TestMethod]
            public async Task failed_natvis_retry_keeps_the_error_instead_of_skipping_an_element()
            {
                int rootReads = 0;
                debuggerMock.GetExpressionCallback = name =>
                {
                    Assert.AreEqual("points", name);
                    if (++rootReads == 1) return new ExpressionMock("{ size=1 }", "std::vector<DemoPoint>", () => "", 1);
                    throw new COMException();
                };
                interpreterMock.GetDrawableCallback = expression => new NeoWatch.Common.Result<IDrawable>(
                    new NeoWatch.Common.Feedback(NeoWatch.Common.FeedbackType.ExpressionParsingException));

                var result = await loader.Load(new WatchItem { Name = "points" });

                Assert.IsTrue(result.Feedback.HasError);
                Assert.AreEqual(0, result.Data.Count);
                Assert.AreEqual(2, rootReads);
            }

            [TestMethod]
            public async Task nested_natvis_retry_uses_parent_and_child_indices_not_the_flat_drawable_index()
            {
                bool yielded = false;
                int reads = 0;
                loader.YieldAction = () => { yielded = true; return Task.CompletedTask; };
                debuggerMock.GetExpressionCallback = name =>
                {
                    Assert.AreEqual("storage", name);
                    bool fresh = ++reads > 1;
                    var tail = new List<IExpression>();
                    for (int i = 1; i <= 101; i++) tail.Add(Leaf(i, fresh));
                    return Container("std::vector<Node>",
                        Container("Node", Leaf(0, fresh)), Container("Node", tail.ToArray()));
                };
                interpreterMock.GetDrawableCallback = expression =>
                {
                    if (yielded && !expression.Value.StartsWith("fresh ", StringComparison.Ordinal))
                        return ParseError("incomplete coordinate");
                    return new NeoWatch.Common.Result<IDrawable>(
                        new DrawablePoint(int.Parse(expression.Value.Split(' ')[1]), 2));
                };

                var result = await loader.Load(new WatchItem { Name = "storage" });

                Assert.IsFalse(result.Feedback.HasError);
                Assert.AreEqual(102, result.Data.Count);
                for (int i = 0; i < result.Data.Count; i++) Assert.AreEqual(new DrawablePoint(i, 2), result.Data[i]);
                Assert.AreEqual(3, reads);
            }

            [TestMethod]
            public async Task synthetic_children_of_a_non_indexable_list_keep_their_natvis_view()
            {
                int reads = 0;
                debuggerMock.GetExpressionCallback = name =>
                {
                    Assert.AreEqual("chainNodes", name);
                    return Container("DemoPointLinkedList", Leaf(7, ++reads > 1));
                };
                interpreterMock.GetDrawableCallback = expression => expression.Value == "fresh 7"
                    ? new NeoWatch.Common.Result<IDrawable>(new DrawablePoint(7, 2))
                    : ParseError("Pnt: (7,)");

                var result = await loader.Load(new WatchItem { Name = "chainNodes" });

                Assert.IsFalse(result.Feedback.HasError);
                Assert.AreEqual(new DrawablePoint(7, 2), result.Data[0]);
                Assert.AreEqual(2, reads);
            }

            [DataTestMethod]
            [DataRow("root-type")]
            [DataRow("parent-type")]
            [DataRow("leaf-type")]
            [DataRow("missing-parent")]
            [DataRow("missing-leaf")]
            [DataRow("invalid-leaf")]
            [DataRow("still-incomplete")]
            [DataRow("parse-com-failure")]
            public async Task failed_nested_retry_never_substitutes_another_element_or_hides_the_original_error(string failure)
            {
                int reads = 0;
                debuggerMock.GetExpressionCallback = name =>
                {
                    var leaf = Leaf(7, ++reads > 1);
                    var parent = Container("Node", leaf);
                    var root = Container("std::vector<Node>", parent);
                    if (reads == 1) return root;
                    switch (failure)
                    {
                        case "root-type": return Container("OtherRoot", parent);
                        case "parent-type": return Container("std::vector<Node>", Container("OtherParent", leaf));
                        case "leaf-type": return Container("std::vector<Node>", Container("Node", new ExpressionMock("fresh 7", "OtherLeaf", () => "")));
                        case "missing-parent": root.DataMembers = new ExpressionsMock(0); break;
                        case "missing-leaf": parent.DataMembers = new ExpressionsMock(0); break;
                        case "invalid-leaf": leaf.IsValidValue = false; break;
                    }
                    return root;
                };
                interpreterMock.GetDrawableCallback = expression =>
                {
                    if (expression.Value == "old 7") return ParseError("Pnt: (7,)");
                    if (failure == "parse-com-failure") throw new COMException();
                    return failure == "still-incomplete" ? ParseError("different retry error")
                        : new NeoWatch.Common.Result<IDrawable>(new DrawablePoint(7, 2));
                };

                var result = await loader.Load(new WatchItem { Name = "storage" });

                Assert.IsTrue(result.Feedback.HasError);
                StringAssert.Contains(result.Feedback.Detail, "Pnt: (7,)");
                Assert.AreEqual(0, result.Data.Count);
                Assert.AreEqual(2, reads);
            }

            [TestMethod]
            public void reset_debug_session_clears_process_specific_reader_state()
            {
                var memory = new MemoryReaderMock();
                var loader = new Loader(new DebuggerMock(), new InterpreterMock(), memory);

                loader.ResetDebugSession();

                Assert.AreEqual(1, memory.ResetCount);
            }

            private static ExpressionMock Leaf(int index, bool fresh)
            {
                return new ExpressionMock((fresh ? "fresh " : "old ") + index, "Point", () => "");
            }

            private static ExpressionMock Container(string type, params IExpression[] children)
            {
                return new ExpressionMock("List", type, () => "") { DataMembers = new ExpressionsMock(children) };
            }

            private static NeoWatch.Common.Result<IDrawable> ParseError(string value)
            {
                return new NeoWatch.Common.Result<IDrawable>(new NeoWatch.Common.Feedback(
                    NeoWatch.Common.FeedbackType.ExpressionParsingException, value));
            }
        }

        [TestClass]
        public class Load_LinkedListFromMemory
        {
            private const string Blueprint =
@"[DemoSegmentLinkedList]
Count=Count
Head=Head
Next=Next
Tag=demoSegment.type|Int32
Line.Tag=0
Line.InitialX=demoSegment.segment.line.demoInitialPoint.demoX|Float64
Line.InitialY=demoSegment.segment.line.demoInitialPoint.demoY|Float64
Line.FinalX=demoSegment.segment.line.demoFinalPoint.demoX|Float64
Line.FinalY=demoSegment.segment.line.demoFinalPoint.demoY|Float64
Arc.Tag=1
Arc.CenterX=demoSegment.segment.arc.demoCenterPoint.demoX|Float64
Arc.CenterY=demoSegment.segment.arc.demoCenterPoint.demoY|Float64
Arc.Radius=demoSegment.segment.arc.demoRadius|Float64
Arc.InitialAngle=demoSegment.segment.arc.demoInitialAngle|Float64
Arc.SweepAngle=demoSegment.segment.arc.demoSweepAngle|Float64";

            private const string PointBlueprint =
@"[DemoPointLinkedList]
Count=Count
Head=Head
Next=Next
Point.X=x|Float32
Point.Y=y|Float32";

            [TestMethod]
            public async Task returns_empty_recognised_list_without_natvis_expansion()
            {
                var debugger = new DebuggerMock { CurrentProcessId = 42 };
                int expandedCalls = 0;
                debugger.GetExpressionWithOptionsCallback = (expression, useAutoExpandRules) =>
                {
                    if (useAutoExpandRules) expandedCalls++;
                    if (expression == "emptyChain")
                    {
                        return new ExpressionMock(string.Empty, "DemoPointLinkedList", () => string.Empty);
                    }
                    if (expression == "(emptyChain).Count")
                    {
                        return new ExpressionMock("0", "int", () => string.Empty);
                    }
                    throw new COMException();
                };

                var loader = new Loader(debugger, new InterpreterMock(), new MemoryReaderMock());
                loader.ConfigureLinkedListMemoryLoading(true, PointBlueprint);

                var result = await loader.Load(new WatchItem { Name = "emptyChain" });

                Assert.IsFalse(result.Feedback.HasError);
                Assert.AreEqual(0, result.Data.Count);
                Assert.AreEqual(0, expandedCalls);
            }

            [TestMethod]
            public async Task decodes_line_and_arc_without_interpreting_natvis_elements()
            {
                var debugger = new DebuggerMock { CurrentProcessId = 42 };
                var memory = new MemoryReaderMock();
                var interpreter = new InterpreterMock();
                int interpreterCalls = 0;
                interpreter.GetDrawableCallback = expression =>
                {
                    interpreterCalls++;
                    return new NeoWatch.Common.Result<IDrawable>(new DrawablePoint(99, 99));
                };

                ConfigureDebugger(debugger);
                memory.SetMemory(0x1000, LineNode(0x2000, 1, 2, 3, 4));
                memory.SetMemory(0x2000, ArcNode(0, 5, 6, 7, 8, 9));

                var loader = new Loader(debugger, interpreter, memory);
                loader.ConfigureLinkedListMemoryLoading(true, Blueprint);
                var item = new WatchItem { Name = "mixedChain" };

                var result = await loader.Load(item);

                Assert.IsFalse(result.Feedback.HasError);
                Assert.AreEqual(0, interpreterCalls);
                Assert.AreEqual(2, result.Data.Count);

                var line = result.Data[0] as DrawableLineSegment;
                Assert.IsNotNull(line);
                Assert.AreEqual(1f, line.InitialPoint.X);
                Assert.AreEqual(2f, line.InitialPoint.Y);
                Assert.AreEqual(3f, line.FinalPoint.X);
                Assert.AreEqual(4f, line.FinalPoint.Y);

                var arc = result.Data[1] as DrawableArcSegment;
                Assert.IsNotNull(arc);
                Assert.AreEqual(5f, arc.CenterPoint.X);
                Assert.AreEqual(6f, arc.CenterPoint.Y);
                Assert.AreEqual(7f, arc.Radius);
                Assert.AreEqual(8f, arc.InitialAngle);
                Assert.AreEqual(9f, arc.SweepAngle);

                Assert.IsNotNull(item.Snapshot);
                Assert.IsTrue(item.Snapshot.IsSegmented);
                Assert.AreEqual(2, item.Snapshot.Count);
                Assert.AreEqual(ReloadScope.Nothing, loader.PlanReload(item).Scope);
            }

            [TestMethod]
            public async Task decodes_custom_point_chain_from_its_blueprint()
            {
                var debugger = new DebuggerMock { CurrentProcessId = 42 };
                debugger.GetExpressionCallback = expression =>
                {
                    if (expression == "chainNodes")
                    {
                        return new ExpressionMock("List of Points", "DemoPointLinkedList",
                            () => "parse", 2);
                    }
                    if (expression == "(chainNodes).Count") return Value("2");
                    if (expression == "(void*)((chainNodes).Head)") return Value("0x3000");
                    if (expression == "sizeof(*((chainNodes).Head))") return Value("24");
                    if (expression == "sizeof(void*)") return Value("8");
                    if (expression.StartsWith("(long long)", StringComparison.Ordinal))
                    {
                        if (expression.Contains("->Next")) return Value("0");
                        if (expression.Contains("->x")) return Value("16");
                        if (expression.Contains("->y")) return Value("20");
                    }

                    throw new COMException();
                };

                var memory = new MemoryReaderMock();
                memory.SetMemory(0x3000, PointNode(0x4000, 1.5f, 2.5f));
                memory.SetMemory(0x4000, PointNode(0, 3.5f, 4.5f));

                var loader = new Loader(debugger, new InterpreterMock(), memory);
                loader.ConfigureLinkedListMemoryLoading(true, PointBlueprint);
                var item = new WatchItem { Name = "chainNodes" };

                var result = await loader.Load(item);

                Assert.IsFalse(result.Feedback.HasError);
                Assert.AreEqual(2, result.Data.Count);
                Assert.AreEqual(1.5f, ((DrawablePoint)result.Data[0]).X);
                Assert.AreEqual(2.5f, ((DrawablePoint)result.Data[0]).Y);
                Assert.AreEqual(3.5f, ((DrawablePoint)result.Data[1]).X);
                Assert.AreEqual(4.5f, ((DrawablePoint)result.Data[1]).Y);
                Assert.AreEqual(ReloadScope.Nothing, loader.PlanReload(item).Scope);
            }

            [TestMethod]
            public async Task falls_back_to_natvis_when_a_memory_read_fails()
            {
                var debugger = new DebuggerMock { CurrentProcessId = 42 };
                var memory = new MemoryReaderMock();
                var interpreter = new InterpreterMock();
                ConfigureDebugger(debugger, dataMembers: 1);

                var loader = new Loader(debugger, interpreter, memory);
                loader.ConfigureLinkedListMemoryLoading(true, Blueprint);

                var result = await loader.Load(new WatchItem { Name = "mixedChain" });

                Assert.IsFalse(result.Feedback.HasError);
                Assert.AreEqual(1, result.Data.Count);
                Assert.IsInstanceOfType(result.Data[0], typeof(DrawablePoint));
            }

            [TestMethod]
            public async Task reuses_changed_node_bytes_instead_of_reading_the_list_twice()
            {
                var debugger = new DebuggerMock { CurrentProcessId = 42 };
                var memory = new MemoryReaderMock();
                var interpreter = new InterpreterMock();
                ConfigureDebugger(debugger);
                memory.SetMemory(0x1000, LineNode(0x2000, 1, 2, 3, 4));
                memory.SetMemory(0x2000, ArcNode(0, 5, 6, 7, 8, 9));

                var loader = new Loader(debugger, interpreter, memory);
                loader.ConfigureLinkedListMemoryLoading(true, Blueprint);
                var item = new WatchItem { Name = "mixedChain" };
                await loader.Load(item);

                int readsAfterLoad = memory.ReadCount;
                memory.SetMemory(0x2000, ArcNode(0, 5, 6, 70, 8, 9));

                ReloadPlan plan = loader.PlanReload(item);
                Assert.AreEqual(ReloadScope.Partial, plan.Scope);
                Assert.AreEqual(readsAfterLoad + 2, memory.ReadCount);

                List<DrawableReplacement> replacements = loader.ReloadElements(
                    item, plan.ChangedIndices, plan);

                Assert.AreEqual(readsAfterLoad + 2, memory.ReadCount);
                Assert.AreEqual(1, replacements.Count);
                Assert.AreEqual(1, replacements[0].Index);
                Assert.AreEqual(70f, ((DrawableArcSegment)replacements[0].Drawable).Radius);
            }

            [TestMethod]
            public async Task reloads_everything_when_the_linked_list_topology_changes()
            {
                var debugger = new DebuggerMock { CurrentProcessId = 42 };
                var memory = new MemoryReaderMock();
                ConfigureDebugger(debugger);
                memory.SetMemory(0x1000, LineNode(0x2000, 1, 2, 3, 4));
                memory.SetMemory(0x2000, ArcNode(0, 5, 6, 7, 8, 9));

                var loader = new Loader(debugger, new InterpreterMock(), memory);
                loader.ConfigureLinkedListMemoryLoading(true, Blueprint);
                var item = new WatchItem { Name = "mixedChain" };
                await loader.Load(item);

                memory.SetMemory(0x1000, LineNode(0x3000, 1, 2, 3, 4));

                Assert.AreEqual(ReloadScope.Everything, loader.PlanReload(item).Scope);
            }

            [TestMethod]
            public async Task never_reuses_a_snapshot_from_another_process()
            {
                var debugger = new DebuggerMock { CurrentProcessId = 42 };
                var memory = new MemoryReaderMock();
                ConfigureDebugger(debugger);
                memory.SetMemory(0x1000, LineNode(0x2000, 1, 2, 3, 4));
                memory.SetMemory(0x2000, ArcNode(0, 5, 6, 7, 8, 9));

                var loader = new Loader(debugger, new InterpreterMock(), memory);
                loader.ConfigureLinkedListMemoryLoading(true, Blueprint);
                var item = new WatchItem { Name = "mixedChain" };
                await loader.Load(item);
                int readsBeforeNewProcess = memory.ReadCount;

                debugger.CurrentProcessId = 43;
                ReloadPlan plan = loader.PlanReload(item);

                Assert.AreEqual(ReloadScope.Everything, plan.Scope);
                Assert.AreEqual(readsBeforeNewProcess, memory.ReadCount);
            }

            [TestMethod]
            public async Task keeps_using_natvis_when_the_experimental_mode_is_disabled()
            {
                var debugger = new DebuggerMock { CurrentProcessId = 42 };
                var memory = new MemoryReaderMock();
                var interpreter = new InterpreterMock();
                int interpreterCalls = 0;
                interpreter.GetDrawableCallback = expression =>
                {
                    interpreterCalls++;
                    return new NeoWatch.Common.Result<IDrawable>(new DrawablePoint(10, 20));
                };
                ConfigureDebugger(debugger, dataMembers: 1);
                memory.SetMemory(0x1000, LineNode(0x2000, 1, 2, 3, 4));
                memory.SetMemory(0x2000, ArcNode(0, 5, 6, 7, 8, 9));

                var loader = new Loader(debugger, interpreter, memory);
                loader.ConfigureLinkedListMemoryLoading(false, Blueprint);

                var result = await loader.Load(new WatchItem { Name = "mixedChain" });

                Assert.IsFalse(result.Feedback.HasError);
                Assert.AreEqual(1, interpreterCalls);
                Assert.AreEqual(1, result.Data.Count);
            }

            [TestMethod]
            public void ignores_invalid_blueprint_sections()
            {
                List<LinkedListMemoryBlueprint> parsed = LinkedListMemoryBlueprintParser.Parse(
                    "[Broken]\nCount=Count\nHead=Head\nNext=Next");

                Assert.AreEqual(0, parsed.Count);
            }

            private static void ConfigureDebugger(DebuggerMock debugger, int dataMembers = 0)
            {
                debugger.GetExpressionCallback = expression =>
                {
                    if (expression == "mixedChain")
                    {
                        return new ExpressionMock("List of Line/Arc Segments", "DemoSegmentLinkedList",
                            () => "parse", dataMembers);
                    }
                    if (expression == "(mixedChain).Count") return Value("2");
                    if (expression == "(void*)((mixedChain).Head)") return Value("0x1000");
                    if (expression == "sizeof(*((mixedChain).Head))") return Value("64");
                    if (expression == "sizeof(void*)") return Value("8");

                    if (expression.StartsWith("(long long)", StringComparison.Ordinal))
                    {
                        if (expression.Contains("->Next")) return Value("0");
                        if (expression.Contains("demoSegment.type")) return Value("16");
                        if (expression.Contains("demoInitialPoint.demoX")) return Value("24");
                        if (expression.Contains("demoInitialPoint.demoY")) return Value("32");
                        if (expression.Contains("demoFinalPoint.demoX")) return Value("40");
                        if (expression.Contains("demoFinalPoint.demoY")) return Value("48");
                        if (expression.Contains("demoCenterPoint.demoX")) return Value("24");
                        if (expression.Contains("demoCenterPoint.demoY")) return Value("32");
                        if (expression.Contains("demoInitialAngle")) return Value("40");
                        if (expression.Contains("demoSweepAngle")) return Value("48");
                        if (expression.Contains("demoRadius")) return Value("56");
                    }

                    // The fallback snapshot path uses the original conventional expressions.
                    if (expression == "mixedChain.Count") return Value("2");
                    if (expression == "(void*)(mixedChain.Head)") return Value("0x1000");
                    if (expression == "sizeof(*(mixedChain.Head))") return Value("64");
                    if (expression == "(void*)(mixedChain.Head->Next)") return Value("0x2000");

                    throw new COMException();
                };
            }

            private static ExpressionMock Value(string value)
            {
                return new ExpressionMock(value, "any", () => "parse");
            }

            private static byte[] LineNode(ulong next, double x1, double y1, double x2, double y2)
            {
                var bytes = new byte[64];
                Write(bytes, 0, next);
                Write(bytes, 16, 0);
                Write(bytes, 24, x1);
                Write(bytes, 32, y1);
                Write(bytes, 40, x2);
                Write(bytes, 48, y2);
                return bytes;
            }

            private static byte[] ArcNode(ulong next, double x, double y, double radius,
                double initialAngle, double sweepAngle)
            {
                var bytes = new byte[64];
                Write(bytes, 0, next);
                Write(bytes, 16, 1);
                Write(bytes, 24, x);
                Write(bytes, 32, y);
                Write(bytes, 40, initialAngle);
                Write(bytes, 48, sweepAngle);
                Write(bytes, 56, radius);
                return bytes;
            }

            private static byte[] PointNode(ulong next, float x, float y)
            {
                var bytes = new byte[24];
                Write(bytes, 0, next);
                Buffer.BlockCopy(BitConverter.GetBytes(x), 0, bytes, 16, sizeof(float));
                Buffer.BlockCopy(BitConverter.GetBytes(y), 0, bytes, 20, sizeof(float));
                return bytes;
            }

            private static void Write(byte[] target, int offset, ulong value)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, target, offset, sizeof(ulong));
            }

            private static void Write(byte[] target, int offset, int value)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, target, offset, sizeof(int));
            }

            private static void Write(byte[] target, int offset, double value)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, target, offset, sizeof(double));
            }
        }

        [TestClass]
        public class PlanReload_LinkedList
        {
            private Loader loader;
            private DebuggerMock debuggerMock;
            private InterpreterMock interpreterMock;
            private MemoryReaderMock memoryReaderMock;
            private WatchItem item;

            public PlanReload_LinkedList()
            {
                debuggerMock = new DebuggerMock();
                interpreterMock = new InterpreterMock();
                memoryReaderMock = new MemoryReaderMock();
                loader = new Loader(debuggerMock, interpreterMock, memoryReaderMock);
                item = new WatchItem { Name = "mixedChain" };

                ConfigureDebugger();
                ConfigureMemory(1, 2, 3);
            }

            [TestMethod]
            public async Task returns_nothing_when_custom_linked_list_nodes_have_not_changed()
            {
                await loader.Load(item);

                var plan = loader.PlanReload(item);

                Assert.AreEqual(ReloadScope.Nothing, plan.Scope);
            }

            [TestMethod]
            public async Task returns_everything_when_custom_linked_list_node_bytes_change()
            {
                await loader.Load(item);
                ConfigureMemory(1, 9, 3);

                var plan = loader.PlanReload(item);

                Assert.AreEqual(ReloadScope.Everything, plan.Scope);
            }

            private void ConfigureDebugger()
            {
                var values = new Dictionary<string, string>
                {
                    { "mixedChain", "List of Line/Arc Segments" },
                    { "mixedChain.Count", "3" },
                    { "sizeof(*(mixedChain.Head))", "4" },
                    { "(void*)(mixedChain.Head)", "0x1000" },
                    { "(void*)(mixedChain.Head->Next)", "0x2000" },
                    { "(void*)(mixedChain.Head->Next->Next)", "0x3000" }
                };

                debuggerMock.GetExpressionCallback = name =>
                {
                    string value;
                    if (!values.TryGetValue(name, out value))
                    {
                        throw new COMException();
                    }

                    int members = name == "mixedChain" ? 3 : 0;
                    string type = name == "mixedChain" ? "DemoSegmentLinkedList" : "any";
                    return new ExpressionMock(value, type, () => "parse", members);
                };
            }

            private void ConfigureMemory(byte first, byte second, byte third)
            {
                memoryReaderMock.SetMemory(0x1000, new byte[] { first, 0, 0, 0 });
                memoryReaderMock.SetMemory(0x2000, new byte[] { second, 0, 0, 0 });
                memoryReaderMock.SetMemory(0x3000, new byte[] { third, 0, 0, 0 });
            }
        }

        [TestClass]
        public class WatchItem_PreviousDrawables
        {
            [TestMethod]
            public void toggles_between_current_and_previous_at_the_same_selected_index()
            {
                var currentFirst = new DrawablePoint(1, 1);
                var currentSecond = new DrawablePoint(2, 2);
                var previousFirst = new DrawablePoint(1, 1);
                var previousSecond = new DrawablePoint(3, 3);
                var item = new WatchItem();
                item.Drawables.AddAndNotify(new List<IDrawable> { currentFirst, currentSecond });
                item.SetSelectedItemQuietly(currentSecond);
                item.RememberPreviousDrawables(new List<IDrawable> { previousFirst, previousSecond });

                item.SetShowingPrevious(true);

                Assert.AreSame(item.PreviousDrawables, item.DisplayedDrawables);
                Assert.AreSame(previousSecond, item.SelectedItem);
                Assert.AreSame(previousSecond, item.PreviousDrawables.SelectedItem);
                Assert.AreSame(currentSecond, item.Drawables.SelectedItem);
                Assert.IsFalse(item.IsDrawableChanged(previousFirst));
                Assert.IsTrue(item.IsDrawableChanged(previousSecond));

                item.SetShowingPrevious(false);

                Assert.AreSame(item.Drawables, item.DisplayedDrawables);
                Assert.AreSame(currentSecond, item.SelectedItem);
                Assert.AreSame(currentSecond, item.Drawables.SelectedItem);
                Assert.AreSame(previousSecond, item.PreviousDrawables.SelectedItem);
                Assert.IsFalse(item.IsDrawableChanged(currentFirst));
                Assert.IsTrue(item.IsDrawableChanged(currentSecond));
            }

            [TestMethod]
            public void clears_all_visual_and_memory_state_when_the_debug_session_ends()
            {
                var current = new DrawablePoint(2, 2);
                var previous = new DrawablePoint(1, 1);
                var item = new WatchItem();
                item.Drawables.AddAndNotify(new List<IDrawable> { current });
                item.SetSelectedItemQuietly(current);
                item.RememberPreviousDrawables(new List<IDrawable> { previous });
                item.SetShowingPrevious(true);
                item.Snapshot = new MemorySnapshot(0x1000, null, 8, 1, true,
                    new byte[8], 42);

                item.ClearDebugSessionState();

                Assert.AreEqual(0, item.Drawables.Count);
                Assert.AreEqual(0, item.PreviousDrawables.Count);
                Assert.IsNull(item.SelectedItem);
                Assert.IsNull(item.Snapshot);
                Assert.IsFalse(item.IsShowingPrevious);
                Assert.IsFalse(item.HasPreviousDrawables);
            }
        }
    }
}
