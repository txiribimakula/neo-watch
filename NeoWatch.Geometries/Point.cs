namespace NeoWatch.Geometries
{
    public class Point : IGeometry
    {
        public float X { get; set; }
        public float Y { get; set; }

        public GeometryType Type => GeometryType.Point;

        public Point(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}
