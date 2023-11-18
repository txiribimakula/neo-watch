using NeoWatch.Loading;
using System.Runtime.InteropServices;

namespace Tests.Mocks
{
    public class ExpressionMock : IExpression
    {
        private string _value;

        public ExpressionMock(string value)
        {
            _value = value;
        }

        public string Type => throw new NotImplementedException();

        public string Value => _value;

        public string Name => throw new NotImplementedException();

        public string Parse => throw new COMException();

        public IExpressions DataMembers => throw new NotImplementedException();
    }
}
