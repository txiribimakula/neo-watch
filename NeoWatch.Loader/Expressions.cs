using EnvDTE;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NeoWatch.Loading
{
    public class Expressions : IEnumerable<Expression>
    {
        public Expressions(Expression expression, string[] listTypes)
        {
            _expression = expression;
            _listTypes = listTypes;
        }

        private Expression _expression;
        private string[] _listTypes;

        private bool _isList
        {
            get
            {
                var expressionType = _expression.Type;
                if (_listTypes.Any(listType => expressionType.StartsWith(listType)))
                {
                    return true;
                }

                Match match = Regex.Match(expressionType, @"\w\[\d+\]");
                if (match.Success)
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
