using System;

namespace MiRomWeb.Application.System.Services
{
    /// <summary>
    /// Token过期异常
    /// </summary>
    public class TokenExpiredException : Exception
    {
        public TokenExpiredException(string message) : base(message)
        {
        }

        public TokenExpiredException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
