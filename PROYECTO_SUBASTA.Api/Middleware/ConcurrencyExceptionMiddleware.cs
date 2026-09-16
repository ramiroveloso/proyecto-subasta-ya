using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace PROYECTO_SUBASTA.API.Middlewares
{
    public class ConcurrencyExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        public ConcurrencyExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Deja pasar la petición al siguiente eslabón (controladores / pipeline)
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // 1. Detectar si es un error de concurrencia / transitorio -> 409 Conflict
            bool isConcurrencyError = exception is DbUpdateConcurrencyException ||
                                      exception.Message.Contains("transient", StringComparison.OrdinalIgnoreCase) ||
                                      exception.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase) ||
                                      exception.ToString().Contains("transient", StringComparison.OrdinalIgnoreCase) ||
                                      (exception.InnerException != null && exception.InnerException.Message.Contains("transient", StringComparison.OrdinalIgnoreCase));

            // 2. Detectar si es un error de argumentos / reglas de negocio -> 400 Bad Request
            bool isArgumentError = exception is ArgumentException;

            var statusCode = isConcurrencyError ? HttpStatusCode.Conflict :
                             isArgumentError ? HttpStatusCode.BadRequest :
                             HttpStatusCode.InternalServerError;

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            // Mensaje dinámico según el tipo de excepción capturada
            var responseMessage = isConcurrencyError
                ? "Conflicto de concurrencia (409): La subasta fue modificada por otro usuario en simultáneo. Intente nuevamente."
                : isArgumentError
                ? exception.Message // Mantiene el mensaje exacto que arrojó la regla de negocio (ej. "El nombre de la categoría es obligatorio")
                : "Ocurrió un error inesperado al procesar la solicitud.";

            var errorResponse = new
            {
                status = (int)statusCode,
                error = statusCode.ToString(),
                mensaje = responseMessage
            };

            var jsonResponse = JsonSerializer.Serialize(errorResponse);
            await context.Response.WriteAsync(jsonResponse);
        }
    }
}