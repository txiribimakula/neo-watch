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

            // Only a miss on the type pattern itself is worth retrying through the Parse member:
            // that means the display string was not in a shape we recognise at all. A geometry that
            // WAS identified and then failed to parse is a genuine error, and sending it down the
            // fallback replaces it with "Parse missing", hiding the value that actually broke.
            if (ShouldRetryThroughParse(newDrawableResult))
            {
                try
                {
                    newDrawableResult = GetDrawable(expression.Parse, PatternKind.Type);
                }
                catch (COMException)
                {
                    // Naming the value that failed. On its own "Parse" says only that a fallback
                    // was tried, not which element reached here nor what it looked like — and the
                    // value is exactly what tells you whether the pattern or the NatVis is at fault.
                    return new Result<IDrawable>(new Feedback(FeedbackType.ExpressionLoadException,
                        nameof(expression.Parse) + " missing, value was: " + Shorten(expressionValue)));
                }
            }

            // Whatever the failure was, say which value produced it. Without this the message names
            // the kind of problem but never the data, which is where the answer usually is.
            if (newDrawableResult != null && newDrawableResult.Feedback.HasError
                && newDrawableResult.Feedback.Instance == null)
            {
                newDrawableResult.Feedback.Instance = Shorten(expressionValue);
            }

            return newDrawableResult;
        }

        private static bool ShouldRetryThroughParse(Result<IDrawable> result)
        {
            if (result == null) return true;
            if (!result.Feedback.HasError) return false;

            return result.Feedback.Type == FeedbackType.ExpressionPatternMissmatch
                || result.Feedback.Type == FeedbackType.TypeNotFound;
        }

        /// <summary>Keeps a runaway display string from filling the Status column.</summary>
        private static string Shorten(string value)
        {
            if (value == null) return "null";
            return value.Length <= 60 ? value : value.Substring(0, 60) + "...";
        }

        private Result<IDrawable> GetDrawable(string value, PatternKind patternKind)
        {
            if (value == null)
            {
                return new Result<IDrawable>(new Feedback(FeedbackType.ExpressionPatternMissmatch, "null"));
            }

            var match = Matcher.GetMatch(value, Patterns[patternKind]);
            if (!match.Success)
            {
                return new Result<IDrawable>(new Feedback(FeedbackType.ExpressionPatternMissmatch, value));
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
                    return new Result<IDrawable>(new Feedback(FeedbackType.TypeNotFound));
                }

                switch (kind)
                {
                    case PatternKind.Point:
                        var pointResult = PointInterpreter.ToDrawable(parse, Patterns);
                        return new Result<IDrawable>(pointResult.Data, pointResult.Feedback);
                    case PatternKind.Segment:
                        var segmentResult = LineSegmentInterpreter.ToDrawable(parse, Patterns);
                        return new Result<IDrawable>(segmentResult.Data, segmentResult.Feedback);
                    case PatternKind.Arc:
                        var arcResult = ArcSegmentInterpreter.ToDrawable(parse, Patterns);
                        return new Result<IDrawable>(arcResult.Data, arcResult.Feedback);
                    case PatternKind.Circle:
                        var circleResult = CircleInterpreter.ToDrawable(parse, Patterns);
                        return new Result<IDrawable>(circleResult.Data, circleResult.Feedback);
                    default:
                        return new Result<IDrawable>(new Feedback(FeedbackType.TypeNotFound));
                }
            }
            // unit test this situation
            catch (FormatException ex)
            {
                return null;
            }
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
