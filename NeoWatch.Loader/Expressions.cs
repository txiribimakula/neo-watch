using EnvDTE;
using System.Collections;
using System.Collections.Generic;

namespace NeoWatch.Loading
{
    public class Expressions : IEnumerable<Expression>
    {
        public Expressions(Expression expression)
        {
            _expression = expression;
        }

        private Expression _expression;

        public IEnumerator<Expression> GetEnumerator()
        {
            var currentIndex = 0;
            foreach (Expression currentExpression in _expression.DataMembers)
            {
                var currentExpressionName = currentExpression.Name;
                if (currentExpressionName.Equals("[" + currentIndex + "]"))
                {
                    currentIndex++;
                    yield return currentExpression;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
