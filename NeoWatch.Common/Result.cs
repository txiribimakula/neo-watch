namespace NeoWatch.Common
{
    public class Result<T>
    {
        public Result(FeedbackType feedback)
        {
            Feedback = new Feedback(feedback);
        }

        public Result(T data, FeedbackType feedback = FeedbackType.OK)
        {
            Data = data;
            Feedback = new Feedback(feedback);
        }

        public T Data { get; set; }

        public Feedback Feedback { get; private set; }
    }
}