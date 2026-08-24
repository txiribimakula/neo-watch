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
        }

        private IExpression _expression;
        private string[] _listTypes;

        /// <summary>
        /// Exposed so the loader can tell a container from a single geometry: the memory
        /// snapshot of C0 only applies to containers, where the win is.
        /// </summary>
        public bool IsList
        {
            get { return _isList; }
        }

        /// <summary>
        /// True only when the type really is an indexable container, so that <c>v[0]</c> is a valid
        /// expression. Narrower than <see cref="IsList"/> on purpose: that one also says yes to a
        /// NatVis that merely displays the word "List" while synthesising its children — a linked
        /// list, or a rectangle expanded into segments — and those have no <c>operator[]</c> and no
        /// contiguous block to take the address of.
        /// </summary>
        public bool IsIndexableContainer
        {
            get
            {
                var expressionType = _expression.Type;
                return isInListTypes(expressionType) || isMatchingListPattern(expressionType);
            }
        }

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
