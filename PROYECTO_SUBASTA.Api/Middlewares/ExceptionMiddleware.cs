using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PROYECTO_SUBASTA.Application.Constants;
using PROYECTO_SUBASTA.Application.DTOs;
using PROYECTO_SUBASTA.Application.Exceptions;
using PROYECTO_SUBASTA.Application.UseCases;
using PROYECTO_SUBASTA.Domain.Exceptions;

namespace PROYECTO_SUBASTA.Api.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InvokeAsync(HttpContext context, IAuditoriaService auditoriaService)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex, auditoriaService);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception, IAuditoriaService auditoriaService)
        {
            context.Response.ContentType = "application/json";

            var statusCode = HttpStatusCode.InternalServerError;
            string mensaje = "Se produjo un error interno inesperado en el servidor.";

            switch (exception)
            {
                case ConcurrenciaException ex:
                    statusCode = HttpStatusCode.Conflict;
                    mensaje = ex.Message;
                    _logger.LogWarning(ex, "Conflicto de concurrencia optimista (409) en {Path}: {Mensaje}", context.Request.Path, ex.Message);
                    try
                    {
                        await auditoriaService.RegistrarFalloAsync(
                            AuditoriaAcciones.PUJA_RECHAZADA_CONCURRENCIA,
                            $"Conflicto de concurrencia detectado en ruta {context.Request.Path}: {mensaje}",
                            null
                        );
                    }
                    catch (Exception auditEx)
                    {
                        _logger.LogError(auditEx, "Error al registrar auditoría de fallo por concurrencia en ExceptionMiddleware.");
                    }
                    break;

                case DbUpdateException ex:
                    statusCode = HttpStatusCode.Conflict;
                    mensaje = "Conflicto de integridad de datos. La operación no pudo completarse debido a una restricción en la base de datos.";
                    _logger.LogError(ex, "Conflicto de base de datos (DbUpdateException) en {Path}: {Detalle}", context.Request.Path, ex.InnerException?.Message ?? ex.Message);
                    break;

                case DominioException or ReglaNegocioException:
                    statusCode = HttpStatusCode.BadRequest;
                    mensaje = exception.Message;
                    _logger.LogWarning("Violación de regla de negocio o dominio ({Tipo}) en {Path}: {Mensaje}", exception.GetType().Name, context.Request.Path, exception.Message);
                    break;

                case ArgumentException ex:
                    statusCode = HttpStatusCode.BadRequest;
                    mensaje = ex.Message;
                    _logger.LogWarning("Argumento inválido en {Path}: {Mensaje}", context.Request.Path, ex.Message);
                    break;

                case RecursoNoEncontradoException or KeyNotFoundException:
                    statusCode = HttpStatusCode.NotFound;
                    mensaje = exception.Message;
                    _logger.LogInformation("Recurso no encontrado (404) en {Path}: {Mensaje}", context.Request.Path, exception.Message);
                    break;

                case UnauthorizedAccessException ex:
                    statusCode = HttpStatusCode.Unauthorized;
                    mensaje = ex.Message;
                    _logger.LogWarning("Acceso no autorizado (401) en {Path}: {Mensaje}", context.Request.Path, ex.Message);
                    break;

                default:
                    statusCode = HttpStatusCode.InternalServerError;
                    mensaje = "Se produjo un error interno inesperado en el servidor.";
                    _logger.LogError(exception, "Error no controlado (500) en {Path}: {Mensaje}", context.Request.Path, exception.Message);
                    break;
            }

            context.Response.StatusCode = (int)statusCode;

            var errorResponse = new ErrorResponse
            {
                StatusCode = (int)statusCode,
                Mensaje = mensaje,
                Timestamp = DateTime.UtcNow
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
