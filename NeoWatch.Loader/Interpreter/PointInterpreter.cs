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
    public static class PointInterpreter
    {
        public static Result<DrawablePoint> ToDrawable(string parse, Dictionary<PatternKind, string[]> patterns)
        {
            var match = Matcher.GetMatch(parse, patterns[PatternKind.Point]);

            if (!match.Success)
            {
                return new Result<DrawablePoint>(new Feedback(FeedbackType.ExpressionPatternMissmatch));
            }

            string xParse = match.Groups["x"].Value;
            string yParse = match.Groups["y"].Value;

            float x;
            if (!float.TryParse(xParse, NumberStyles.Float, CultureInfo.InvariantCulture, out x))
            {
                return new Result<DrawablePoint>(new Feedback(FeedbackType.ExpressionParsingException));
            }
            float y;
            if (!float.TryParse(yParse, NumberStyles.Float, CultureInfo.InvariantCulture, out y))
            {
                return new Result<DrawablePoint>(new Feedback(FeedbackType.ExpressionParsingException));
            }

            return new Result<DrawablePoint>(new DrawablePoint(x, y));
        }
    }
}
