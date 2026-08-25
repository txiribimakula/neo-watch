using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoWatch.Drawing;
using NeoWatch.Loading;
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
        }
    }
}
