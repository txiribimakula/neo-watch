using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System;
using NeoWatch.Drawing;
using System.Runtime.InteropServices;
using NeoWatch.Common;

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

        public Result<IDrawable> GetDrawable(IExpression expression)
        {
            var expressionValue = expression.Value;
            var newDrawableResult = GetDrawable(expressionValue, PatternKind.Type);

            if (newDrawableResult.Feedback.Type != FeedbackType.OK)
            {
                try
                {
                    newDrawableResult = GetDrawable(expression.Parse, PatternKind.Type);
                }
                catch (COMException)
                {
                    return new Result<IDrawable>(FeedbackType.ExpressionLoadException);
                }
            }

            return newDrawableResult;
        }

        private Result<IDrawable> GetDrawable(string value, PatternKind patternKind)
        {
            if (value == null)
            {
                return new Result<IDrawable>(FeedbackType.ExpressionPatternMissmatch);
            }

            var match = GetMatch(value, Patterns[patternKind]);
            if (!match.Success)
            {
                return new Result<IDrawable>(FeedbackType.ExpressionPatternMissmatch);
            }

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
                    return new Result<IDrawable>(FeedbackType.TypeNotFound);
                }

                try
                {
                    switch (kind)
                    {
                        case PatternKind.Point:
                            var pointResult = GetDrawablePoint(parse);
                            return new Result<IDrawable>(pointResult.Data, pointResult.Feedback);
                        case PatternKind.Segment:
                            var segmentResult = GetDrawableSegment(parse);
                            return new Result<IDrawable>(segmentResult.Data, segmentResult.Feedback);
                        case PatternKind.Arc:
                            var arcResult = GetDrawableArc(parse);
                            return new Result<IDrawable>(arcResult.Data, arcResult.Feedback);
                        case PatternKind.Circle:
                            var circleResult = GetDrawableCircle(parse);
                            return new Result<IDrawable>(circleResult.Data, circleResult.Feedback);
                        default:
                            return new Result<IDrawable>(FeedbackType.TypeNotFound);
                    }
                }
                catch (DrawableException ex)
                {
                    return new Result<IDrawable>(FeedbackType.UnhandledException);
                }
            }
            // unit test this situation
            catch (FormatException ex)
            {
                return null;
            }
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

        private Result<DrawableArcSegment> GetDrawableCircle(string parse)
        {
            // TODO: handle exceptions for additional feedback when regex is wrong (too many ")"...).
            var match = GetMatch(parse, Patterns[PatternKind.Circle]);

            if (!match.Success)
            {
                return new Result<DrawableArcSegment>(FeedbackType.ExpressionPatternMissmatch);
            }

            string centerPointParse = match.Groups["centerPoint"].Value;
            if (string.IsNullOrEmpty(centerPointParse))
            {
                return new Result<DrawableArcSegment>(FeedbackType.ExpressionPatternMissmatch);
            }
            string radiusParse = match.Groups["radius"].Value;
            if (string.IsNullOrEmpty(radiusParse))
            {
                return new Result<DrawableArcSegment>(FeedbackType.ExpressionPatternMissmatch);
            }

            var centerPointResult = GetDrawablePoint(centerPointParse);
            float radius;
            if (!float.TryParse(radiusParse, NumberStyles.Float, CultureInfo.InvariantCulture, out radius))
            {
                return new Result<DrawableArcSegment>(centerPointResult.Feedback);
            }

            var drawableCircle = new DrawableArcSegment(centerPointResult.Data, 0, 360, radius);
            return new Result<DrawableArcSegment>(drawableCircle);
        }

        private Result<DrawableArcSegment> GetDrawableArc(string parse)
        {
            var match = GetMatch(parse, Patterns[PatternKind.Arc]);

            if (!match.Success)
            {
                return new Result<DrawableArcSegment>(FeedbackType.ExpressionPatternMissmatch);
            }

            string centerPointParse = match.Groups["centerPoint"].Value;
            if (string.IsNullOrEmpty(centerPointParse))
            {
                return new Result<DrawableArcSegment>(FeedbackType.ExpressionPatternMissmatch);
            }
            string radiusParse = match.Groups["radius"].Value;
            if (string.IsNullOrEmpty(radiusParse))
            {
                return new Result<DrawableArcSegment>(FeedbackType.ExpressionPatternMissmatch);
            }
            string initialAngleParse = match.Groups["initialAngle"].Value;
            if (string.IsNullOrEmpty(initialAngleParse))
            {
                return new Result<DrawableArcSegment>(FeedbackType.ExpressionPatternMissmatch);
            }
            string sweepAngleParse = match.Groups["sweepAngle"].Value;
            if (string.IsNullOrEmpty(sweepAngleParse))
            {
                return new Result<DrawableArcSegment>(FeedbackType.ExpressionPatternMissmatch);
            }

            var centerPointResult = GetDrawablePoint(centerPointParse);
            if (centerPointResult.Feedback.Type != FeedbackType.OK)
            {
                return new Result<DrawableArcSegment>(centerPointResult.Feedback);
            }
            float radius;
            if (!float.TryParse(radiusParse, NumberStyles.Float, CultureInfo.InvariantCulture, out radius))
            {
                return new Result<DrawableArcSegment>(FeedbackType.ExpressionParsingException);
            }
            float initialAngle;
            if (!float.TryParse(initialAngleParse, NumberStyles.Float, CultureInfo.InvariantCulture, out initialAngle))
            {
                return new Result<DrawableArcSegment>(FeedbackType.ExpressionParsingException);
            }
            float sweepAngle;
            if (!float.TryParse(sweepAngleParse, NumberStyles.Float, CultureInfo.InvariantCulture, out sweepAngle))
            {
                return new Result<DrawableArcSegment>(FeedbackType.ExpressionParsingException);
            }

            var drawableArcSegment = new DrawableArcSegment(centerPointResult.Data, initialAngle, sweepAngle, radius);
            return new Result<DrawableArcSegment>(drawableArcSegment);
        }

        private Result<DrawableLineSegment> GetDrawableSegment(string parse)
        {
            var match = GetMatch(parse, Patterns[PatternKind.Segment]);

            if (!match.Success)
            {
                return new Result<DrawableLineSegment>(FeedbackType.ExpressionPatternMissmatch);
            }

            string initialPointParse = match.Groups["initialPoint"].Value;
            if (string.IsNullOrEmpty(initialPointParse))
            {
                return new Result<DrawableLineSegment>(FeedbackType.ExpressionPatternMissmatch);
            }
            string finalPointParse = match.Groups["finalPoint"].Value;
            if (string.IsNullOrEmpty(finalPointParse))
            {
                return new Result<DrawableLineSegment>(FeedbackType.ExpressionPatternMissmatch);
            }

            // TODO: differentiate between one point or the other failing.
            var initialPointResult = GetDrawablePoint(initialPointParse);
            if (initialPointResult.Feedback.Type != FeedbackType.OK)
            {
                return new Result<DrawableLineSegment>(initialPointResult.Feedback);
            }
            var finalPointResult = GetDrawablePoint(finalPointParse);
            if (finalPointResult.Feedback.Type != FeedbackType.OK)
            {
                return new Result<DrawableLineSegment>(finalPointResult.Feedback);
            }

            var drawableSegment = new DrawableLineSegment(initialPointResult.Data, finalPointResult.Data);
            return new Result<DrawableLineSegment>(drawableSegment);
        }

        private Result<DrawablePoint> GetDrawablePoint(string parse)
        {
            var match = GetMatch(parse, Patterns[PatternKind.Point]);

            if (!match.Success)
            {
                return new Result<DrawablePoint>(FeedbackType.ExpressionPatternMissmatch);
            }

            string xParse = match.Groups["x"].Value;
            string yParse = match.Groups["y"].Value;

            float x;
            if (!float.TryParse(xParse, NumberStyles.Float, CultureInfo.InvariantCulture, out x))
            {
                return new Result<DrawablePoint>(FeedbackType.ExpressionParsingException);
            }
            float y;
            if (!float.TryParse(yParse, NumberStyles.Float, CultureInfo.InvariantCulture, out y))
            {
                return new Result<DrawablePoint>(FeedbackType.ExpressionParsingException);
            }

            return new Result<DrawablePoint>(new DrawablePoint(x, y));
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
