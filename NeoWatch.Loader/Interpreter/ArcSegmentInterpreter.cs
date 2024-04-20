using NeoWatch.Common;
using NeoWatch.Drawing;
using NeoWatch.Loading;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeoWatch.Loading
{
    public static class ArcSegmentInterpreter
    {
        public static Result<DrawableArcSegment> ToDrawable(string parse, Dictionary<PatternKind, string[]> patterns)
        {
            var match = Matcher.GetMatch(parse, patterns[PatternKind.Arc]);

            if (!match.Success)
            {
                return new Result<DrawableArcSegment>(new Feedback(FeedbackType.ExpressionPatternMissmatch));
            }

            string centerPointParse = match.Groups["centerPoint"].Value;
            if (string.IsNullOrEmpty(centerPointParse))
            {
                return new Result<DrawableArcSegment>(new Feedback(FeedbackType.ExpressionPatternMissmatch));
            }
            string radiusParse = match.Groups["radius"].Value;
            if (string.IsNullOrEmpty(radiusParse))
            {
                return new Result<DrawableArcSegment>(new Feedback(FeedbackType.ExpressionPatternMissmatch));
            }
            string initialAngleParse = match.Groups["initialAngle"].Value;
            if (string.IsNullOrEmpty(initialAngleParse))
            {
                return new Result<DrawableArcSegment>(new Feedback(FeedbackType.ExpressionPatternMissmatch));
            }
            string sweepAngleParse = match.Groups["sweepAngle"].Value;
            if (string.IsNullOrEmpty(sweepAngleParse))
            {
                return new Result<DrawableArcSegment>(new Feedback(FeedbackType.ExpressionPatternMissmatch));
            }

            var centerPointResult = PointInterpreter.ToDrawable(centerPointParse, patterns);
            if (centerPointResult.Feedback.HasError)
            {
                return new Result<DrawableArcSegment>(centerPointResult.Feedback);
            }
            float radius;
            if (!float.TryParse(radiusParse, NumberStyles.Float, CultureInfo.InvariantCulture, out radius))
            {
                return new Result<DrawableArcSegment>(new Feedback(FeedbackType.ExpressionParsingException));
            }
            float initialAngle;
            if (!float.TryParse(initialAngleParse, NumberStyles.Float, CultureInfo.InvariantCulture, out initialAngle))
            {
                return new Result<DrawableArcSegment>(new Feedback(FeedbackType.ExpressionParsingException));
            }
            float sweepAngle;
            if (!float.TryParse(sweepAngleParse, NumberStyles.Float, CultureInfo.InvariantCulture, out sweepAngle))
            {
                return new Result<DrawableArcSegment>(new Feedback(FeedbackType.ExpressionParsingException));
            }

            var drawableArcSegment = new DrawableArcSegment(centerPointResult.Data, initialAngle, sweepAngle, radius);
            return new Result<DrawableArcSegment>(drawableArcSegment);
        }
    }
}
