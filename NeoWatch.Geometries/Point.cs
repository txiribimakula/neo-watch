using System;

namespace NeoWatch.Geometries
{
    public class Point : IGeometry, IEquatable<Point>
    {
        public float X { get; set; }
        public float Y { get; set; }

        public GeometryType Type => GeometryType.Point;

        public Point(float x, float y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(Point other)
        {
            if (ReferenceEquals(this, other)) return true;

            if (!X.EqualsWithTolerance(other.X)) return false;
            if (!Y.EqualsWithTolerance(other.Y)) return false;

            return true;
        }
    }
}
