using System;

namespace PROYECTO_SUBASTA.Application.Exceptions
{
    public class ReglaNegocioException : Exception
    {
        public ReglaNegocioException() { }
        public ReglaNegocioException(string message) : base(message) { }
        public ReglaNegocioException(string message, Exception inner) : base(message, inner) { }
    }
}
