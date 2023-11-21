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

        public string Detail
        {
            get
            {
                switch (Type)
                {
                    case FeedbackType.OK:
                        return "OK.";
                    case FeedbackType.CoordinatesInitError:
                        return "Coordinates init error.";
                    case FeedbackType.VariableCouldNotBeLoadedError:
                        return "Variable could not be loaded.";
                    default:
                        return null;
                }
            }
        }
    }

    public enum FeedbackType
    {
        OK,
        CoordinatesInitError,
        VariableCouldNotBeLoadedError
    }
}
