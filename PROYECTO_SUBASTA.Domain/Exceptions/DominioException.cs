using System;

namespace PROYECTO_SUBASTA.Domain.Exceptions
{
    public class DominioException : InvalidOperationException
    {
        public DominioException(string message) : base(message)
        {
        }

        public DominioException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
