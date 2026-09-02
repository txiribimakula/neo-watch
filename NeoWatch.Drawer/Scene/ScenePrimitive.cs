using System;
using System.Runtime.InteropServices;

namespace NeoWatch.Drawing.Scene
{
    public enum PrimitiveKind { Point, Line, Arc }

    // Value-only snapshot: never retain mutable geometry or debugger objects in a renderer.
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ScenePrimitive
    {
        public readonly double X, Y, EndX, EndY, Radius, StartAngle, SweepAngle;
        public readonly PrimitiveKind Kind;

        public ScenePrimitive(PrimitiveKind kind, double x, double y, double endX = 0,
            double endY = 0, double radius = 0, double startAngle = 0, double sweepAngle = 0)
        {
            Kind = kind;
            X = x; Y = y; EndX = endX; EndY = endY;
            Radius = radius; StartAngle = startAngle; SweepAngle = sweepAngle;
        }

        public static ScenePrimitive Copy(IDrawable drawable)
        {
            if (drawable is DrawablePoint point)
                return new ScenePrimitive(PrimitiveKind.Point, point.X, point.Y);
            if (drawable is DrawableLineSegment line)
                return new ScenePrimitive(PrimitiveKind.Line, line.InitialPoint.X, line.InitialPoint.Y,
                    line.FinalPoint.X, line.FinalPoint.Y);
            if (drawable is DrawableArcSegment arc)
                return new ScenePrimitive(PrimitiveKind.Arc, arc.CenterPoint.X, arc.CenterPoint.Y,
                    radius: arc.Radius, startAngle: arc.InitialAngle, sweepAngle: arc.SweepAngle);
            throw new NotSupportedException("Unsupported canvas primitive: " + drawable?.GetType().Name);
        }

        public bool IsFinite => Finite(X) && Finite(Y) && Finite(EndX) && Finite(EndY)
            && Finite(Radius) && Radius >= 0 && Finite(StartAngle) && Finite(SweepAngle);

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public SceneBounds Bounds
        {
            get
            {
                if (!IsFinite) return SceneBounds.Unbounded;
                if (Kind == PrimitiveKind.Arc)
                {
                    if (Math.Abs(SweepAngle) >= 360 || Math.Abs(StartAngle) > 36000)
                        return new SceneBounds(X - Radius, Y - Radius, X + Radius, Y + Radius);
                    var bounds = ArcPoint(StartAngle).Union(ArcPoint(StartAngle + SweepAngle));
                    for (int angle = 0; angle < 360; angle += 90)
                    {
                        double delta = SweepAngle >= 0 ? angle - StartAngle : StartAngle - angle;
                        delta = ((delta % 360) + 360) % 360;
                        if (delta <= Math.Abs(SweepAngle)) bounds = bounds.Union(ArcPoint(angle));
                    }
                    return bounds;
                }
                if (Kind == PrimitiveKind.Line)
                    return new SceneBounds(Math.Min(X, EndX), Math.Min(Y, EndY),
                        Math.Max(X, EndX), Math.Max(Y, EndY));
                return new SceneBounds(X, Y, X, Y);
            }
        }

        private SceneBounds ArcPoint(double angle)
        {
            double radians = angle * Math.PI / 180;
            double x = X + Radius * Math.Cos(radians), y = Y + Radius * Math.Sin(radians);
            // Roundoff at quadrant boundaries must not make the spatial bound too small.
            double margin = Math.Max(1, Math.Max(Math.Abs(x), Math.Abs(y))) * 1e-14;
            return new SceneBounds(x - margin, y - margin, x + margin, y + margin);
        }
    }

    public readonly struct SceneBounds
    {
        public readonly double MinX, MinY, MaxX, MaxY;
        public SceneBounds(double minX, double minY, double maxX, double maxY)
        { MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY; }
        public static SceneBounds Empty => new SceneBounds(double.PositiveInfinity, double.PositiveInfinity,
            double.NegativeInfinity, double.NegativeInfinity);
        public static SceneBounds Unbounded => new SceneBounds(double.NegativeInfinity, double.NegativeInfinity,
            double.PositiveInfinity, double.PositiveInfinity);
        public bool IsEmpty => MinX > MaxX || MinY > MaxY;
        public SceneBounds Union(SceneBounds other) => new SceneBounds(Math.Min(MinX, other.MinX),
            Math.Min(MinY, other.MinY), Math.Max(MaxX, other.MaxX), Math.Max(MaxY, other.MaxY));
        public bool Intersects(SceneBounds other) => !IsEmpty && !other.IsEmpty &&
            MinX <= other.MaxX && MaxX >= other.MinX && MinY <= other.MaxY && MaxY >= other.MinY;
    }
}
