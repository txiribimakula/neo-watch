using System;

namespace NeoWatch.Loading
{
    public class LoadingException : Exception
    {
        public LoadingException(string message): base(message) {
        }
    }

    public class MemberNotFoundException: LoadingException
    {
        public MemberNotFoundException(string type, string memberNme): base(memberNme + " was not found in type " + type) {
        }
    }
}
