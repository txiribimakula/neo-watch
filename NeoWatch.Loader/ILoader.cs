using EnvDTE;
using System.Collections.Generic;
using NeoWatch.Geometries;

namespace Txiribimakula.ExpertWatch.Loading
{
    public interface ILoading
    {
        List<IGeometry> Load(Expression expression);
    }
}
