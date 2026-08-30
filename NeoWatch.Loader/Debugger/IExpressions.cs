using System.Collections.Generic;

namespace NeoWatch.Loading
{
    public interface IExpressions : IEnumerable<IExpression>
    {
        // Gets the numbered NatVis child without changing its view.
        IExpression GetAt(int index);
    }
}
