namespace NeoWatch.Geometries
{
    public class LineSegment : IGeometry
    {
        public LineSegment(Point initialPoint, Point finalPoint) {
            InitialPoint = initialPoint;
            FinalPoint = finalPoint;
        }

        public Point InitialPoint { get; set; }
        public Point FinalPoint { get; set; }

        public GeometryType Type => GeometryType.Line;
    }
}