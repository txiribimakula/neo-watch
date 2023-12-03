using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System;
using NeoWatch.Drawing;
using System.Runtime.InteropServices;

namespace NeoWatch.Loading
{
    public class Interpreter : IInterpreter
    {
        public Interpreter(Dictionary<PatternKind, string[]> patterns, Dictionary<string, PatternKind> typeKindPairs)
        {
            Patterns = patterns;
            TypeKindPairs = typeKindPairs;
        }

        public Dictionary<PatternKind, string[]> Patterns { get; set; }

        public Dictionary<string, PatternKind> TypeKindPairs { get; set; }

        public IDrawable GetDrawable(IExpression expression)
        {
            var expressionValue = expression.Value;
            var newDrawable = GetDrawable(expressionValue, PatternKind.Type);

            if (newDrawable == null)
            {
                try
                {
                    newDrawable = GetDrawable(expression.Parse, PatternKind.Type);
                }
                catch (COMException)
                {
                    return null;
                }
            }

            return newDrawable;
        }

        private IDrawable GetDrawable(string value, PatternKind patternKind)
        {
            if (value == null)
            {
                return null;
            }

            var match = GetMatch(value, Patterns[patternKind]);

            if (match.Success)
            {
                try
                {
                    string type = match.Groups["type"].Value;
                    var parse = match.Groups["parse"].Value;

                    PatternKind? kind = null;
                    try
                    {
                        kind = TypeKindPairs[type];
                    }
                    catch (KeyNotFoundException)
                    {
                        return new Drawable("Type is not interpretable.");
                    }

                    try
                    {
                        switch (kind)
                        {
                            case PatternKind.Point:
                                return GetDrawablePoint(parse);
                            case PatternKind.Segment:
                                return GetDrawableSegment(parse);
                            case PatternKind.Arc:
                                return GetDrawableArc(parse);
                            case PatternKind.Circle:
                                return GetDrawableCircle(parse);
                            default:
                                return new Drawable("Type is not interpretable.");
                        }
                    }
                    catch (DrawableException ex)
                    {
                        return ex.Drawable;
                    }
                }
                catch (FormatException ex)
                {
                    return null;
                }
            }
            return null;
        }

        private Match GetMatch(string value, string[] patterns)
        {
            Match match = Match.Empty;

            foreach (var pattern in patterns)
            {
                match = Regex.Match(value, pattern);
                if (match.Success)
                {
                    return match;
                }
            }

            return match;
        }

        private DrawableArcSegment GetDrawableCircle(string parse)
        {
            // TODO: handle exceptions for additional feedback when regex is wrong (too many ")"...).
            var match = GetMatch(parse, Patterns[PatternKind.Circle]);

            if (!match.Success)
            {
                return null;
            }

            string centerPointParse = match.Groups["centerPoint"].Value;
            if (string.IsNullOrEmpty(centerPointParse))
            {
                return null;
            }
            string radiusParse = match.Groups["radius"].Value;
            if (string.IsNullOrEmpty(radiusParse))
            {
                return null;
            }

            var centerPoint = GetDrawablePoint(centerPointParse);
            float radius;
            if (!float.TryParse(radiusParse, NumberStyles.Float, CultureInfo.InvariantCulture, out radius))
            {
                return null;
            }

            return new DrawableArcSegment(centerPoint, 0, 360, radius);
        }

        private DrawableArcSegment GetDrawableArc(string parse)
        {
            var match = GetMatch(parse, Patterns[PatternKind.Arc]);

            if (!match.Success)
            {
                return null;
            }

            string centerPointParse = match.Groups["centerPoint"].Value;
            if (string.IsNullOrEmpty(centerPointParse))
            {
                return null;
            }
            string radiusParse = match.Groups["radius"].Value;
            if (string.IsNullOrEmpty(radiusParse))
            {
                return null;
            }
            string initialAngleParse = match.Groups["initialAngle"].Value;
            if (string.IsNullOrEmpty(initialAngleParse))
            {
                return null;
            }
            string sweepAngleParse = match.Groups["sweepAngle"].Value;
            if (string.IsNullOrEmpty(sweepAngleParse))
            {
                return null;
            }

            var centerPoint = GetDrawablePoint(centerPointParse);
            if (centerPoint == null)
            {
                return null;
            }
            float radius;
            if (!float.TryParse(radiusParse, NumberStyles.Float, CultureInfo.InvariantCulture, out radius))
            {
                return null;
            }
            float initialAngle;
            if (!float.TryParse(initialAngleParse, NumberStyles.Float, CultureInfo.InvariantCulture, out initialAngle))
            {
                return null;
            }
            float sweepAngle;
            if (!float.TryParse(sweepAngleParse, NumberStyles.Float, CultureInfo.InvariantCulture, out sweepAngle))
            {
                return null;
            }

            return new DrawableArcSegment(centerPoint, initialAngle, sweepAngle, radius);
        }

        private DrawableLineSegment GetDrawableSegment(string parse)
        {
            var match = GetMatch(parse, Patterns[PatternKind.Segment]);

            if (!match.Success)
            {
                return null;
            }

            string initialPointParse = match.Groups["initialPoint"].Value;
            if (string.IsNullOrEmpty(initialPointParse))
            {
                return null;
            }
            string finalPointParse = match.Groups["finalPoint"].Value;
            if (string.IsNullOrEmpty(finalPointParse))
            {
                return null;
            }

            // TODO: differentiate between one point or the other failing.
            var initialPoint = GetDrawablePoint(initialPointParse);
            if (initialPoint == null)
            {
                return null;
            }
            var finalPoint = GetDrawablePoint(finalPointParse);
            if (finalPoint == null)
            {
                return null;
            }

            return new DrawableLineSegment(initialPoint, finalPoint);
        }

        private DrawablePoint GetDrawablePoint(string parse)
        {
            var match = GetMatch(parse, Patterns[PatternKind.Point]);

            if (!match.Success)
            {
                return null;
            }

            string xParse = match.Groups["x"].Value;
            string yParse = match.Groups["y"].Value;

            float x;
            if (!float.TryParse(xParse, NumberStyles.Float, CultureInfo.InvariantCulture, out x))
            {
                return null;
            }
            float y;
            if (!float.TryParse(yParse, NumberStyles.Float, CultureInfo.InvariantCulture, out y))
            {
                return null;
            }

            return new DrawablePoint(x, y);
        }
    }

    public enum PatternKind
    {
        Type,
        Point,
        Segment,
        Arc,
        Circle
    }
}
