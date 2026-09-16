using System;

namespace PROYECTO_SUBASTA.Application.Exceptions
{
    public class RecursoNoEncontradoException : Exception
    {
        public RecursoNoEncontradoException() { }
        public RecursoNoEncontradoException(string message) : base(message) { }
        public RecursoNoEncontradoException(string message, Exception inner) : base(message, inner) { }
    }
}
