using System.Collections;
using System.Collections.Generic;
using DTE = EnvDTE;

namespace NeoWatch.Loading
{
    public class Expressions : IExpressions
    {
        DTE::Expressions _expressions;

        public Expressions(DTE::Expressions expressions)
        {
            _expressions = expressions;
        }

        public IEnumerator<IExpression> GetEnumerator()
        {
            var currentIndex = 0;
            foreach (DTE::Expression currentExpression in _expressions)
            {
                var currentExpressionName = currentExpression.Name;
                if (currentExpressionName.Equals("[" + currentIndex + "]"))
                {
                    currentIndex++;
                    yield return new Expression(currentExpression);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new System.NotImplementedException();
        }
    }
}
