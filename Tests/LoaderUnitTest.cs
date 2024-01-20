using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoWatch.Loading;
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
            [DataRow(2)]
            [DataRow(3)]
            public async Task returns_result_even_if_max_is_reached(int maxItems)
            {
                // Arrange
                ExpressionMock.GetParse getParse = () => "parse";
                debuggerMock.GetExpressionCallback = new DebuggerMock.Callback((string name) => new ExpressionMock("List", "type", getParse, 3));
                loader = new Loader(debuggerMock, interpreterMock, maxItems);

                // Act
                var result = await loader.Load(new WatchItem());

                // Assert
                Assert.IsNotNull(result.Data);
                Assert.AreEqual(maxItems, result.Data.Count);
            }
        }
    }
}
