using System;

namespace NeoWatch.Geometries
{
    public class LineSegment : IGeometry, IEquatable<LineSegment>
    {
        public LineSegment(Point initialPoint, Point finalPoint) {
            InitialPoint = initialPoint;
            FinalPoint = finalPoint;
        }

        public Point InitialPoint { get; set; }
        public Point FinalPoint { get; set; }

        public GeometryType Type => GeometryType.Line;

        public bool Equals(LineSegment other)
        {
            if (ReferenceEquals(this, other)) return true;

            if (!InitialPoint.Equals(other.InitialPoint)) return false;
            if (!FinalPoint.Equals(other.FinalPoint)) return false;

            return true;
        }
    }
}