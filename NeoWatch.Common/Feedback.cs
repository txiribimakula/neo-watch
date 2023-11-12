namespace NeoWatch.Common
{
    public class Feedback
    {
        private bool hasError = false;

        public Feedback()
        {
            Description = "OK";
        }

        public Feedback(string description)
        {
            Description = description;
            hasError = true;
        }

        public bool HasError => hasError;

        public string Description { get; private set; }
    }
}
