using NeoWatch.Loading;
using System.Runtime.InteropServices;
using Tests.Mocks;

namespace Tests
{
    public class LoaderUnitTest
    {
        public class Load_Item
        {
            private Loader loader;
            private DebuggerMock debuggerMock;

            public Load_Item()
            {
                debuggerMock = new DebuggerMock();
                loader = new Loader(debuggerMock, new InterpreterMock());
            }

            [Fact]
            public async void returns_result_with_could_not_load_message_when_getexpression_throws_comexception()
            {
                // Arrange
                debuggerMock.GetExpressionCallback = new DebuggerMock.Callback((string name) => throw new COMException());

                // Act
                var result = await loader.Load(new WatchItem());

                // Assert
                Assert.Equal("Variable could not be loaded", result.Feedback.Detail);
            }
        }
    }
}
