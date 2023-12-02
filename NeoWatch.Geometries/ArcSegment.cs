using System;

namespace NeoWatch.Geometries
{
    public class ArcSegment : IGeometry, IEquatable<ArcSegment>
    {
        public ArcSegment(Point centerPoint, float initialAngle, float sweepAngle, float radius) {
            CenterPoint = centerPoint;
            InitialAngle = initialAngle;
            SweepAngle = sweepAngle;
            Radius = radius;
            SetInitialAndFinalPoints();
        }

        private void SetInitialAndFinalPoints()
        {
            InitialPoint = new Point(
                CenterPoint.X + (float)Math.Cos((InitialAngle / 180.0) * Math.PI) * Radius,
                CenterPoint.Y + (float)Math.Sin((InitialAngle / 180.0) * Math.PI) * Radius);

            FinalPoint = new Point(
               CenterPoint.X + (float)Math.Cos(((InitialAngle + SweepAngle) / 180.0) * Math.PI) * Radius,
               CenterPoint.Y + (float)Math.Sin(((InitialAngle + SweepAngle) / 180.0) * Math.PI) * Radius);
        }

        public Point InitialPoint { get; set; }
        public Point FinalPoint { get; set; }
        public Point CenterPoint { get; set; }
        public float InitialAngle { get; set; }
        public float SweepAngle { get; set; }
        public float Radius { get; set; }
        public float Diameter => 2 * Radius;

        public GeometryType Type => GeometryType.Arc;

        public override bool Equals(object obj)
        {
            if(obj is ArcSegment objArcSegment)
            {
                return Equals(objArcSegment);
            }

            return false;
        }

        public bool Equals(ArcSegment other)
        {
            if (ReferenceEquals(this, other)) return true;

            if (!InitialPoint.Equals(other.InitialPoint)) return false;
            if (!FinalPoint.Equals(other.FinalPoint)) return false;
            if (!CenterPoint.Equals(other.CenterPoint)) return false;
            if (!InitialAngle.EqualsWithTolerance(other.InitialAngle)) return false;
            if (!SweepAngle.EqualsWithTolerance(other.SweepAngle)) return false;
            if (!Radius.EqualsWithTolerance(other.Radius)) return false;

            return true;
        }

        public override int GetHashCode()
        {
            var hashCode = InitialPoint.GetHashCode();
            hashCode ^= FinalPoint.GetHashCode();
            hashCode ^= CenterPoint.GetHashCode();
            hashCode ^= InitialAngle.GetHashCode();
            hashCode ^= SweepAngle.GetHashCode();
            hashCode ^= Radius.GetHashCode();
            return hashCode;
        }
    }
}
