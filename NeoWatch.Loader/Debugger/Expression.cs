using DTE = EnvDTE;

namespace NeoWatch.Loading
{
    public class Expression : IExpression
    {
        DTE::Expression _expression;

        public Expression(DTE::Expression expression)
        {
            _expression = expression;
        }

        public string Type => _expression.Type;

        public string Value => _expression.Value;

        public string Name => _expression.Name;

        public string Parse => _expression.DataMembers.Item("Parse").Value;

        public IExpressions DataMembers => new Expressions(_expression.DataMembers);
    }
}
