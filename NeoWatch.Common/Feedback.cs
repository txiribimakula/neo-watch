namespace NeoWatch.Common
{
    public class Feedback
    {
        public Feedback(FeedbackType type, string instance = null)
        {
            Type = type;
            Instance = instance;
        }

        public FeedbackType Type { get; private set; }

        public bool HasError => Type != FeedbackType.OK;

        public string Title
        {
            get
            {
                switch (Type)
                {
                    case FeedbackType.OK:
                        return "OK.";
                    case FeedbackType.ExpressionParsingException:
                        return "Expression parsing threw an exception.";
                    case FeedbackType.TypeNotFound:
                        return "Missing valid expression Type.";
                    case FeedbackType.ExpressionPatternMissmatch:
                        return "Expression does not match any valid patterns.";
                    case FeedbackType.ExpressionLoadException:
                        return "Expression could not be loaded.";
                    case FeedbackType.Cancelled:
                        return "Loading was cancelled by the user.";
                    default:
                        return null;
                }
            }
        }

        public string Instance { get; set; }

        public string Detail => Title + (Instance == null ? string.Empty : " (" + Instance + ")");
    }

    public enum FeedbackType
    {
        OK,
        ExpressionParsingException,
        TypeNotFound,
        ExpressionPatternMissmatch,
        ExpressionLoadException,
        Cancelled
    }
}
