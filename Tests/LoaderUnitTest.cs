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
