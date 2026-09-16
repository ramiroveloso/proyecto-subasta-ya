using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PROYECTO_SUBASTA.Application.Exceptions;

namespace PROYECTO_SUBASTA.Api.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var statusCode = HttpStatusCode.InternalServerError;
            string mensaje = "Se produjo un error inesperado en el servidor.";

            switch (exception)
            {
                case DbUpdateConcurrencyException ex:
                    statusCode = HttpStatusCode.Conflict;
                    mensaje = "Conflicto de concurrencia (409): La subasta fue modificada por otro usuario en simultáneo. Intente nuevamente.";
                    _logger.LogWarning("Conflicto de concurrencia optimista detectado por EF Core: {Mensaje}", ex.Message);
                    break;

                case ConcurrenciaException ex:
                    statusCode = HttpStatusCode.Conflict;
                    mensaje = ex.Message;
                    _logger.LogWarning("Conflicto de concurrencia: {Mensaje}", ex.Message);
                    break;

                case RecursoNoEncontradoException ex:
                    statusCode = HttpStatusCode.NotFound;
                    mensaje = ex.Message;
                    _logger.LogInformation("Recurso no encontrado: {Mensaje}", ex.Message);
                    break;

                case KeyNotFoundException ex:
                    statusCode = HttpStatusCode.NotFound;
                    mensaje = ex.Message;
                    _logger.LogInformation("Clave no encontrada: {Mensaje}", ex.Message);
                    break;

                case ReglaNegocioException ex:
                    statusCode = HttpStatusCode.BadRequest;
                    mensaje = ex.Message;
                    _logger.LogWarning("Violación de regla de negocio: {Mensaje}", ex.Message);
                    break;

                case ArgumentException ex:
                    statusCode = HttpStatusCode.BadRequest;
                    mensaje = ex.Message;
                    _logger.LogWarning("Argumento inválido: {Mensaje}", ex.Message);
                    break;

                case InvalidOperationException ex:
                    statusCode = HttpStatusCode.BadRequest;
                    mensaje = ex.Message;
                    _logger.LogWarning("Operación inválida: {Mensaje}", ex.Message);
                    break;

                default:
                    statusCode = HttpStatusCode.InternalServerError;
                    _logger.LogError(exception, "Error no controlado capturado en ExceptionMiddleware: {Mensaje}", exception.Message);
                    break;
            }

            context.Response.StatusCode = (int)statusCode;

            var errorResponse = new
            {
                statusCode = (int)statusCode,
                mensaje = mensaje,
                timestamp = DateTime.UtcNow
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, jsonOptions));
        }
    }
}
