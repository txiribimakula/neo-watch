using NeoWatch.Loading;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Tests.Mocks.ExpressionMock;

namespace Tests.Mocks
{
    public class ExpressionsMock : IExpressions
    {
        private List<IExpression> _expressions = new List<IExpression>();

        public ExpressionsMock(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _expressions.Add(new ExpressionMock("value", "type", () => "parse"));
            }
        }

        public IEnumerator<IExpression> GetEnumerator()
        {
            return _expressions.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _expressions.GetEnumerator();
        }
    }
}
