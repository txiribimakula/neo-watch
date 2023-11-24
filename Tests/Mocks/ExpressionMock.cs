using NeoWatch.Loading;

namespace Tests.Mocks
{
    public class ExpressionMock : IExpression
    {
        private string _value;
        public delegate string GetParse();
        private GetParse getParse;

        public ExpressionMock(string value, GetParse getParse)
        {
            _value = value;
            this.getParse = getParse;
        }

        public string Type => throw new NotImplementedException();

        public string Value => _value;

        public string Name => throw new NotImplementedException();

        public string Parse => getParse();

        public IExpressions DataMembers => throw new NotImplementedException();
    }
}
