namespace NeoWatch.Common
{
    public class Result<T>
    {
        public Result(Feedback feedback)
        {
            Feedback = feedback;
        }

        public Result(T data, Feedback feedback = null)
        {
            Data = data;
            Feedback = feedback ?? new Feedback(FeedbackType.OK);
        }

        public T Data { get; set; }

        public Feedback Feedback { get; private set; }
    }
}