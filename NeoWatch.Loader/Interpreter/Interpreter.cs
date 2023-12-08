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

            if (newDrawableResult.Feedback.HasError)
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

            var match = Matcher.GetMatch(value, Patterns[patternKind]);
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
