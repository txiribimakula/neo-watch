using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NeoWatch.Loading
{
    public class ExpressionLoader : IEnumerable<IExpression>
    {
        public ExpressionLoader(IExpression expression, string[] listTypes)
        {
            _expression = expression;
            _listTypes = listTypes;
            _isList = GetIsList();
        }

        public string Parse { get; set; }

        private IExpression _expression;
        private string[] _listTypes;
        private bool _isList;

        private bool GetIsList()
        {
            var expressionType = _expression.Type;
            if (isInListTypes(expressionType) || isMatchingListPattern(expressionType))
            {
                Parse = expressionType;
                return true;
            }

            var expressionValue = _expression.Value;
            if (expressionValue.Contains("List"))
            {
                Parse = expressionValue;
                return true;
            }

            Parse = $"Single (type: {expressionType} - value: {expressionValue})";
            return false;
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

        public IEnumerator<IExpression> GetEnumerator()
        {
            if (!_isList)
            {
                yield return _expression;
                yield break;
            }

            foreach (IExpression currentExpression in _expression.DataMembers)
            {
                yield return currentExpression;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
