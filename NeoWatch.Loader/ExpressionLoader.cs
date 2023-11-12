using EnvDTE;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NeoWatch.Loading
{
    public class ExpressionLoader : IEnumerable<Expression>
    {
        public ExpressionLoader(Expression expression, string[] listTypes)
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
                if (isInListTypes(expressionType) || isMatchingListPattern(expressionType))
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

        private bool isInListTypes(string expressionType)
        {
            if (_listTypes.Any(listType => expressionType.StartsWith(listType)))
            {
                return true;
            }

            return false;
        }

        private bool isMatchingListPattern(string expressionType)
        {
            Match match = Regex.Match(expressionType, @"\w\[\d+\]");
            if (match.Success)
            {
                return true;
            }

            return false;
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
