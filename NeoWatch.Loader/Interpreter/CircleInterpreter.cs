using NeoWatch.Common;
using NeoWatch.Drawing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeoWatch.Loading
{
    public static class CircleInterpreter
    {
        public static Result<DrawableArcSegment> ToDrawable(string parse, Dictionary<PatternKind, string[]> patterns)
        {
            // TODO: handle exceptions for additional feedback when regex is wrong (too many ")"...).
            var match = Matcher.GetMatch(parse, patterns[PatternKind.Circle]);

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

            var centerPointResult = PointInterpreter.ToDrawable(centerPointParse, patterns);
            float radius;
            if (!float.TryParse(radiusParse, NumberStyles.Float, CultureInfo.InvariantCulture, out radius))
            {
                return new Result<DrawableArcSegment>(centerPointResult.Feedback);
            }

            var drawableCircle = new DrawableArcSegment(centerPointResult.Data, 0, 360, radius);
            return new Result<DrawableArcSegment>(drawableCircle);
        }
    }
}
