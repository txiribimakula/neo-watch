using DTE = EnvDTE;

namespace NeoWatch.Loading
{
    /// <summary>
    /// Wraps a debugger expression. Every property here is a COM round-trip into the expression
    /// evaluator, and the loader asks for some of them more than once per element: Value is read
    /// in <see cref="ExpressionLoader"/> to detect nested lists and again in the interpreter to
    /// parse the geometry.
    ///
    /// A wrapper lives for one element within one load, and the debuggee is stopped for all of
    /// it, so the values cannot change underneath. Reading each at most once takes a container
    /// element from four COM reads down to three.
    /// </summary>
    public class Expression : IExpression
    {
        private readonly DTE::Expression _expression;

        private string _type;
        private bool _typeRead;

        private string _value;
        private bool _valueRead;

        private string _name;
        private bool _nameRead;

        public Expression(DTE::Expression expression)
        {
            _expression = expression;
        }

        public string Type
        {
            get
            {
                if (!_typeRead)
                {
                    _type = _expression.Type;
                    _typeRead = true;
                }
                return _type;
            }
        }

        public string Value
        {
            get
            {
                if (!_valueRead)
                {
                    _value = _expression.Value;
                    _valueRead = true;
                }
                return _value;
            }
        }

        public string Name
        {
            get
            {
                if (!_nameRead)
                {
                    _name = _expression.Name;
                    _nameRead = true;
                }
                return _name;
            }
        }

        public bool IsValidValue => _expression.IsValidValue;

        // Not cached: only read on the fallback path when the main pattern fails, and it throws
        // COMException when the type has no Parse member, which is not worth memoising.
        public string Parse => _expression.DataMembers.Item("Parse").Value;

        public IExpressions DataMembers => new Expressions(_expression.DataMembers);
    }
}
