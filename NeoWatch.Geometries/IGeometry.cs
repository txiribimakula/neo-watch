using System;

namespace NeoWatch.Geometries
{
    public interface IGeometry
    {
        GeometryType Type { get; }
    }

    public enum GeometryType
    {
        Point,
        Line,
        Arc
    }
}