using System;

namespace NeoWatch.Drawing.Scene
{
    public readonly struct SceneCamera
    {
        public readonly double Left, Bottom, PixelsPerUnit, Width, Height;
        public SceneCamera(double left, double bottom, double pixelsPerUnit, double width, double height)
        { Left = left; Bottom = bottom; PixelsPerUnit = pixelsPerUnit; Width = width; Height = height; }
        public static SceneCamera From(ICoordinateSystem coordinates) => new SceneCamera(
            coordinates.LocalMinX, coordinates.LocalMinY, coordinates.ConvertLengthToWorld(1),
            coordinates.WorldWidth, coordinates.WorldHeight);
        public bool IsValid => PixelsPerUnit > 0 && Width > 0 && Height > 0 &&
            Finite(PixelsPerUnit) && Finite(Left) && Finite(Bottom) && Finite(Width) && Finite(Height);
        private static bool Finite(double value) => !double.IsInfinity(value) && !double.IsNaN(value);
        public double ScreenX(double x) => (x - Left) * PixelsPerUnit;
        public double ScreenY(double y) => Height - (y - Bottom) * PixelsPerUnit;
        public SceneBounds VisibleBounds(double marginPixels = 10)
        {
            double margin = marginPixels / PixelsPerUnit;
            return new SceneBounds(Left - margin, Bottom - margin,
                Left + Width / PixelsPerUnit + margin, Bottom + Height / PixelsPerUnit + margin);
        }
    }
}
