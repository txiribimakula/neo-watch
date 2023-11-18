namespace NeoWatch.Loading
{
    public interface IExpression
    {
        string Type { get; }

        string Value { get; }

        string Name { get; }

        string Parse { get; }

        IExpressions DataMembers { get; }
    }
}
