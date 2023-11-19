using NeoWatch.Drawing;
using System.Collections.Generic;

namespace NeoWatch.Loading
{
    public interface IInterpreter
    {
        IDrawable GetDrawable(IExpression expression);

        Dictionary<PatternKind, string[]> Patterns { get; set; }

        Dictionary<string, PatternKind> TypeKindPairs { get; set; }
    }
}
