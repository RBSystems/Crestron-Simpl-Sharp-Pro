using System;

namespace S_100_Template
{
    public class NotRegisteredException : Exception
    {
        public NotRegisteredException(string message)
            : base(message)
        {

        }
    }
}