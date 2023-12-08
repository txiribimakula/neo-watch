namespace NeoWatch.Common
{
    public class Feedback
    {
        public Feedback()
        {
            Type = FeedbackType.OK;
        }

        public Feedback(FeedbackType type)
        {
            Type = type;
        }

        public FeedbackType Type { get; private set; }

        public bool HasError => Type != FeedbackType.OK;

        public string Detail
        {
            get
            {
                switch (Type)
                {
                    case FeedbackType.OK:
                        return "OK.";
                    case FeedbackType.ExpressionValueInvalid:
                        return "Expression Value is not valid.";
                    case FeedbackType.ExpressionParsingException:
                        return "Expression parsing threw an exception.";
                    case FeedbackType.TypeNotFound:
                        return "Missing valid expression Type.";
                    case FeedbackType.ExpressionPatternMissmatch:
                        return "Expression does not match any valid patterns.";
                    case FeedbackType.UnhandledException:
                        // This kind of error should be broken down into known scenarios.
                        return "Unhandled exception.";
                    case FeedbackType.ExpressionLoadException:
                        return "Expression could not be loaded.";
                    case FeedbackType.MaximumElementsCap:
                        // Include arguments in Feedback, for instance: number of maximum elements.
                        return "Maximum elements per item is currently capped because of performance reasons.";
                    default:
                        return null;
                }
            }
        }
    }

    public enum FeedbackType
    {
        OK,
        ExpressionValueInvalid,
        ExpressionParsingException,
        TypeNotFound,
        ExpressionPatternMissmatch,
        UnhandledException,
        ExpressionLoadException,
        MaximumElementsCap
    }
}
