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

        private bool _isList
        {
            get
            {
                var expressionType = _expression.Type;
                if (expressionType.StartsWith("std::vector") || expressionType.StartsWith("System.Collections.Generic.List"))
                {
                    return true;
                }

                var expressionValue = _expression.Value;
                if (expressionValue.Contains("List"))
                {
                    return true;
                }

                return false;
            }
        }

        public IEnumerator<Expression> GetEnumerator()
        {
            if (!_isList)
            {
                yield return _expression;
                yield break;
            }

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
