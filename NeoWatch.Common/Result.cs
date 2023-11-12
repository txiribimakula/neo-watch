namespace NeoWatch.Common
{
    public class Result<T>
    {
        public Result(T data)
        {
            Data = data;
            Feedback = new Feedback();
        }

        public Result(T data, string feedback)
        {
            Data = data;
            Feedback = new Feedback(feedback);
        }

        public T Data { get; set; }

        public Feedback Feedback { get; private set; }
    }
}