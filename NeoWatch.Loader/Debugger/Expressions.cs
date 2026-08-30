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

        public IExpression GetAt(int index)
        {
            if (index < 0 || _expressions == null) return null;
            string name = "[" + index + "]";
            var child = _expressions.Item(name);
            if (child == null) return null;
            var expression = new Expression(child);
            return expression.Name == name ? expression : null;
        }

        public IEnumerator<IExpression> GetEnumerator()
        {
            var currentIndex = 0;
            foreach (DTE::Expression currentExpression in _expressions)
            {
                // Through the wrapper, so this Name read is the cached one rather than a
                // separate COM call: Expression is the only place that talks to the evaluator.
                var expression = new Expression(currentExpression);
                if (expression.Name.Equals("[" + currentIndex + "]"))
                {
                    currentIndex++;
                    yield return expression;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new System.NotImplementedException();
        }
    }
}
