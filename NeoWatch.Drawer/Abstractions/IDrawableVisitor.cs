using NeoWatch.Geometries;

namespace NeoWatch.Drawing
{
    public interface IDrawableVisitor
    {
        LineSegment GetTransformedSegment(LineSegment segment);
        ArcSegment GetTransformedArc(ArcSegment arc);
        Point GetTransformedPoint(Point point);
    }
}
