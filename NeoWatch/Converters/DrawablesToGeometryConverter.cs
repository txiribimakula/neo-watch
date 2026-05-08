using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using NeoWatch.Drawing;
using NeoWatch.Loading;
using GeoPoint = NeoWatch.Geometries.Point;
using GeoLineSegment = NeoWatch.Geometries.LineSegment;
using GeoArcSegment = NeoWatch.Geometries.ArcSegment;
using IDrawable = NeoWatch.Drawing.IDrawable;

namespace NeoWatch.Converters
{
    public enum DrawablesGeometryMode
    {
        Main,
        Selected,
        Caps,
        Points,
        SelectedPoint
    }

    public class DrawablesToGeometryConverter : IMultiValueConverter
    {
        public DrawablesGeometryMode Mode { get; set; } = DrawablesGeometryMode.Main;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0) return Geometry.Empty;

            var drawables = values[0] as DrawableCollection;
            if (drawables == null) return Geometry.Empty;

            IDrawable selected = values.Length > 1 ? values[1] as IDrawable : null;

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
                            AppendPointDot(ctx, (DrawablePoint)drawable);
                            break;
                        case DrawablesGeometryMode.SelectedPoint:
                            if (!isPoint) continue;
                            if (!isSelected) continue;
                            AppendPointDot(ctx, (DrawablePoint)drawable);
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

        private static void AppendPointDot(StreamGeometryContext ctx, DrawablePoint dp)
        {
            var p = dp.TransformedGeometry as GeoPoint;
            if (p == null) return;
            // Degenerate line: BeginFigure + LineTo at the same coordinate.
            // With StrokeStartLineCap/StrokeEndLineCap = Round on the Path, this
            // renders as a filled circle of diameter = StrokeThickness, with no
            // arc tessellation and no stroke-offset computation.
            var wpfPoint = new Point(p.X, p.Y);
            ctx.BeginFigure(wpfPoint, false, false);
            ctx.LineTo(wpfPoint, true, false);
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
