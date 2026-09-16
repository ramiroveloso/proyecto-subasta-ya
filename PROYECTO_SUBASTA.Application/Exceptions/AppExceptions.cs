using System;

namespace PROYECTO_SUBASTA.Application.Exceptions
{
    // Clase(s) específicas de excepción que no duplican definiciones presentes en archivos separados.
    // Las excepciones `ReglaNegocioException` y `RecursoNoEncontradoException` están definidas
    // en sus propios archivos para mantener una clase por archivo. Aquí se mantiene solo
    // la excepción de concurrencia usada para representar conflictos específicos.
    public class ConcurrenciaException : Exception
    {
        public ConcurrenciaException(string message) : base(message)
        {
        }

        public ConcurrenciaException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
