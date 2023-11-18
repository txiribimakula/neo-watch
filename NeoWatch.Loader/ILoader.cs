using System.Collections.Generic;
using NeoWatch.Geometries;

namespace NeoWatch.Loading
{
    public interface ILoading
    {
        List<IGeometry> Load(Expression expression);
    }
}
