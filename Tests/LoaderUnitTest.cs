using NeoWatch.Loading;
using Tests.Mocks;

namespace Tests
{
    public class LoaderUnitTest
    {
        public class Load_Item
        {
            private Loader loader;

            public Load_Item()
            {
                loader = new Loader(new DebuggerMock(), new InterpreterMock());
            }

            [Fact]
            public async void returns_drawables()
            {
                // Arrange


                // Act
                await Lo

                // Assert
            }

        }
    }
}
