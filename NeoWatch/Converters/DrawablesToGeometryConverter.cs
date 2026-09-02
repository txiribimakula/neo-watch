using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using NeoWatch.Drawing;
using GeoPoint = NeoWatch.Geometries.Point;
using GeoLineSegment = NeoWatch.Geometries.LineSegment;
using GeoArcSegment = NeoWatch.Geometries.ArcSegment;
using IDrawable = NeoWatch.Drawing.IDrawable;

namespace NeoWatch.Converters
{
    public enum DrawablesGeometryMode
    {
        Main,
        Unselected,
        Selected,
        Caps,
        Points,
        UnselectedPoints,
        SelectedPoint
    }

    public class DrawablesToGeometryConverter : IMultiValueConverter
    {
        public DrawablesGeometryMode Mode { get; set; } = DrawablesGeometryMode.Main;

        /// <summary>
        /// Side of the square drawn for each point, in device pixels. Only used by the
        /// Points, UnselectedPoints and SelectedPoint modes, whose geometry is filled rather than stroked.
        /// </summary>
        public double DotSize { get; set; } = 4;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0) return Geometry.Empty;

            var drawables = values[0] as DrawableCollection;
            if (drawables == null) return Geometry.Empty;

            // Read from the collection rather than from a second binding: with GeometryVersion
            // as the only source, a reload costs one pass per layer instead of three.
            IDrawable selected = drawables.SelectedItem;

            var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
            using (var ctx = geometry.Open())
            {
                foreach (var drawable in drawables)
                {
                    bool isSelected = ReferenceEquals(drawable, selected);
                    bool isPoint = drawable is DrawablePoint;

                    switch (Mode)
                    {
                        case DrawablesGeometryMode.Main:
                            if (isPoint) continue;
                            if (isSelected && drawables.Count > 1) continue;
                            AppendDrawable(ctx, drawable);
                            break;
                        case DrawablesGeometryMode.Unselected:
                            if (isPoint || isSelected) continue;
                            AppendDrawable(ctx, drawable);
                            break;
                        case DrawablesGeometryMode.Selected:
                            if (isPoint) continue;
                            if (!isSelected) continue;
                            AppendDrawable(ctx, drawable);
                            break;
                        case DrawablesGeometryMode.Caps:
                            AppendCap(ctx, drawable);
                            break;
                        case DrawablesGeometryMode.Points:
                            if (!isPoint) continue;
                            if (isSelected && drawables.Count > 1) continue;
                            AppendPointDot(ctx, (DrawablePoint)drawable, DotSize);
                            break;
                        case DrawablesGeometryMode.UnselectedPoints:
                            if (!isPoint || isSelected) continue;
                            AppendPointDot(ctx, (DrawablePoint)drawable, DotSize);
                            break;
                        case DrawablesGeometryMode.SelectedPoint:
                            if (!isPoint) continue;
                            if (!isSelected) continue;
                            AppendPointDot(ctx, (DrawablePoint)drawable, DotSize);
                            break;
                    }
                }
            }
            geometry.Freeze();
            return geometry;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return null;
        }

        private static void AppendDrawable(StreamGeometryContext ctx, IDrawable drawable)
        {
            switch (drawable)
            {
                case DrawableLineSegment seg:
                    AppendSegment(ctx, seg.TransformedGeometry as GeoLineSegment);
                    break;
                case DrawableArcSegment arc:
                    AppendArc(ctx, arc.TransformedGeometry as GeoArcSegment);
                    break;
            }
        }

        private static void AppendCap(StreamGeometryContext ctx, IDrawable drawable)
        {
            if (drawable is DrawableLineSegment seg)
            {
                AppendSegment(ctx, seg.TransformedCapGeometry as GeoLineSegment);
            }
        }

        private static void AppendPointDot(StreamGeometryContext ctx, DrawablePoint dp, double size)
        {
            var p = dp.TransformedGeometry as GeoPoint;
            if (p == null) return;

            // Axis-aligned filled square, NOT a stroked dot. WPF only has a fast
            // rasterisation path for axis-aligned rectangular figures; round caps,
            // filled circles and even filled octagons all fall into the general
            // tessellator, which is superlinear in the number of figures. Measured on
            // 50.000 points: round caps 5.7 s, octagons 5.6 s, these squares 75 ms.
            // See NeoWatch.Benchmark (`raster` mode) before changing the shape back.
            double half = size / 2.0;
            double left = p.X - half;
            double top = p.Y - half;
            double right = p.X + half;
            double bottom = p.Y + half;

            ctx.BeginFigure(new Point(left, top), true, true);
            ctx.LineTo(new Point(right, top), false, false);
            ctx.LineTo(new Point(right, bottom), false, false);
            ctx.LineTo(new Point(left, bottom), false, false);
        }

        private static void AppendSegment(StreamGeometryContext ctx, GeoLineSegment seg)
        {
            if (seg == null) return;
            ctx.BeginFigure(new Point(seg.InitialPoint.X, seg.InitialPoint.Y), false, false);
            ctx.LineTo(new Point(seg.FinalPoint.X, seg.FinalPoint.Y), true, false);
        }

        private static void AppendArc(StreamGeometryContext ctx, GeoArcSegment arc)
        {
            if (arc == null) return;
            if (Math.Abs(arc.SweepAngle) >= 360)
            {
                var half1 = new GeoArcSegment(arc.CenterPoint, 0, 180, arc.Radius);
                var half2 = new GeoArcSegment(arc.CenterPoint, 180, 180, arc.Radius);
                AppendArcFigure(ctx, half1);
                AppendArcFigure(ctx, half2);
            }
            else
            {
                AppendArcFigure(ctx, arc);
            }
        }

        private static void AppendArcFigure(StreamGeometryContext ctx, GeoArcSegment arc)
        {
            ctx.BeginFigure(new Point(arc.InitialPoint.X, arc.InitialPoint.Y), false, false);
            bool isLargeArc = Math.Abs(arc.SweepAngle) >= 180;
            var dir = arc.SweepAngle > 0 ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;
            ctx.ArcTo(
                new Point(arc.FinalPoint.X, arc.FinalPoint.Y),
                new Size(arc.Radius, arc.Radius),
                arc.SweepAngle,
                isLargeArc,
                dir,
                true,
                false);
        }
    }
}
