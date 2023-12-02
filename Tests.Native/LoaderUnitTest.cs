using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoWatch.Loading;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Tests.Mocks;

namespace Native.Tests
{
    [TestClass]
    public class LoaderUnitTest
    {
        [TestClass]
        public class Load_Item
        {
            private Loader loader;
            private DebuggerMock debuggerMock;

            public Load_Item()
            {
                debuggerMock = new DebuggerMock();
                loader = new Loader(debuggerMock, new InterpreterMock());
            }

            [TestMethod]
            public async Task returns_result_with_could_not_load_message_when_getexpression_throws_comexception()
            {
                // Arrange
                debuggerMock.GetExpressionCallback = new DebuggerMock.Callback((string name) => throw new COMException());

                // Act
                var result = await loader.Load(new WatchItem());

                // Assert
                Assert.AreEqual("Variable could not be loaded.", result.Feedback.Detail);
            }
        }
    }
}
