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
        Caps
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
                    if (Mode == DrawablesGeometryMode.Selected && !isSelected) continue;
                    if (Mode == DrawablesGeometryMode.Main && isSelected && drawables.Count > 1) continue;

                    switch (Mode)
                    {
                        case DrawablesGeometryMode.Main:
                        case DrawablesGeometryMode.Selected:
                            AppendDrawable(ctx, drawable);
                            break;
                        case DrawablesGeometryMode.Caps:
                            AppendCap(ctx, drawable);
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
                case DrawablePoint p:
                    AppendPoint(ctx, p.TransformedGeometry as GeoPoint);
                    break;
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

        private static void AppendPoint(StreamGeometryContext ctx, GeoPoint p)
        {
            if (p == null) return;
            ctx.BeginFigure(new Point(p.X - 2, p.Y), true, true);
            ctx.ArcTo(new Point(p.X + 2, p.Y), new Size(2, 2), 0, true, SweepDirection.Clockwise, true, false);
            ctx.ArcTo(new Point(p.X - 2, p.Y), new Size(2, 2), 0, true, SweepDirection.Clockwise, true, false);
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
