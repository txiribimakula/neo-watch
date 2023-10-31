using NeoWatch.Geometries;

namespace NeoWatch.Drawing
{
    public class DrawableVisitor : IDrawableVisitor
    {
        public ICoordinateSystem CoordinateSystem { get; set; }

        public DrawableVisitor(ICoordinateSystem coordinateSystem) {
            CoordinateSystem = coordinateSystem;
        }

        public LineSegment GetTransformedSegment(LineSegment segment) {
            if(segment != null) {
                Point initialPoint = CoordinateSystem.ConvertPointToWorld(segment.InitialPoint);
                Point finalPoint = CoordinateSystem.ConvertPointToWorld(segment.FinalPoint);
                return new LineSegment(initialPoint, finalPoint);
            }
            return null;
        }

        public ArcSegment GetTransformedArc(ArcSegment arc) {
            var initialPoint = CoordinateSystem.ConvertPointToWorld(arc.InitialPoint);
            var finalPoint = CoordinateSystem.ConvertPointToWorld(arc.FinalPoint);
            var centerPoint = CoordinateSystem.ConvertPointToWorld(arc.CenterPoint);
            float radius = CoordinateSystem.ConvertLengthToWorld(arc.Radius);
            return new ArcSegment(centerPoint, arc.InitialAngle, arc.SweepAngle, radius) {
                InitialPoint = initialPoint,
                FinalPoint = finalPoint
            };
        }

        public Point GetTransformedPoint(Point point) {
            return CoordinateSystem.ConvertPointToWorld(point); ;
        }
    }
}