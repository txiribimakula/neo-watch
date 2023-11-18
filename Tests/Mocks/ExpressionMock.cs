using NeoWatch.Loading;
using System.Runtime.InteropServices;

namespace Tests.Mocks
{
    public class ExpressionMock : IExpression
    {
        private string _value;
        private string _parse;

        public ExpressionMock(string value, string parse = "COMException")
        {
            _value = value;
            _parse = parse;
        }

        public string Type => throw new NotImplementedException();

        public string Value => _value;

        public string Name => throw new NotImplementedException();

        public string Parse
        {
            get {
                switch (_parse)
                {
                    case "COMException":
                        throw new COMException();
                    default:
                        return _parse;
                }
            }
        }

        public IExpressions DataMembers => throw new NotImplementedException();
    }
}
