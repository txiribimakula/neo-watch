using NeoWatch.Common;
using NeoWatch.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeoWatch.Loading
{
    public static class LineSegmentInterpreter
    {
        public static Result<DrawableLineSegment> ToDrawable(string parse, Dictionary<PatternKind, string[]> patterns)
        {
            var match = Matcher.GetMatch(parse, patterns[PatternKind.Segment]);

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
            var initialPointResult = PointInterpreter.ToDrawable(initialPointParse, patterns);
            if (initialPointResult.Feedback.HasError)
            {
                return new Result<DrawableLineSegment>(initialPointResult.Feedback);
            }
            var finalPointResult = PointInterpreter.ToDrawable(finalPointParse, patterns);
            if (finalPointResult.Feedback.HasError)
            {
                return new Result<DrawableLineSegment>(finalPointResult.Feedback);
            }

            var drawableSegment = new DrawableLineSegment(initialPointResult.Data, finalPointResult.Data);
            return new Result<DrawableLineSegment>(drawableSegment);
        }
    }
}
