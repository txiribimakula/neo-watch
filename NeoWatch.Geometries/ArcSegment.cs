using System;

namespace NeoWatch.Geometries
{
    public class ArcSegment : IGeometry
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
    }
}
