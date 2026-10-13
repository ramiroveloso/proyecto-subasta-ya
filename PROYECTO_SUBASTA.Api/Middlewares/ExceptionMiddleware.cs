using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            var ruta = context.Request.Path.Value ?? string.Empty;
            var metodo = context.Request.Method;

            switch (exception)
            {
                case DbUpdateConcurrencyException:
                case ConcurrenciaException:
                    statusCode = HttpStatusCode.Conflict;
                    mensaje = "El recurso fue modificado por otra operación; reintenta.";
                    _logger.LogWarning("Conflicto de concurrencia optimista en {Metodo} {Ruta}: {Mensaje}", metodo, ruta, exception.Message);
                    try
                    {
                        var auditoria = context.RequestServices.GetService<IAuditoriaService>();
                        if (auditoria != null)
                        {
                            await auditoria.RegistrarFalloAsync(
                                "PUJA_RECHAZADA_CONCURRENCIA",
                                $"Conflicto de concurrencia detectado en {metodo} {ruta}: {mensaje}",
                                null
                            );
                        }
                    }
                    catch (Exception exAud)
                    {
                        _logger.LogWarning(exAud, "Error al registrar evento de auditoría de concurrencia en {Ruta}", ruta);
                    }
                    break;

                case RecursoNoEncontradoException ex:
                    statusCode = HttpStatusCode.NotFound;
                    mensaje = ex.Message;
                    _logger.LogInformation("Recurso no encontrado en {Metodo} {Ruta}: {Mensaje}", metodo, ruta, ex.Message);
                    break;

                case KeyNotFoundException ex:
                    statusCode = HttpStatusCode.NotFound;
                    mensaje = ex.Message;
                    _logger.LogInformation("Clave no encontrada en {Metodo} {Ruta}: {Mensaje}", metodo, ruta, ex.Message);
                    break;

                case DominioException ex:
                    statusCode = HttpStatusCode.BadRequest;
                    mensaje = ex.Message;
                    _logger.LogWarning("Violación de regla de dominio en {Metodo} {Ruta}: {Mensaje}", metodo, ruta, ex.Message);
                    break;

                case UnauthorizedAccessException ex:
                    statusCode = HttpStatusCode.Unauthorized;
                    mensaje = ex.Message;
                    _logger.LogWarning("Acceso no autorizado en {Metodo} {Ruta}: {Mensaje}", metodo, ruta, ex.Message);
                    break;

                case ReglaNegocioException ex:
                    statusCode = HttpStatusCode.BadRequest;
                    mensaje = ex.Message;
                    _logger.LogWarning("Violación de regla de negocio en {Metodo} {Ruta}: {Mensaje}", metodo, ruta, ex.Message);
                    break;

                case ArgumentException ex:
                    statusCode = HttpStatusCode.BadRequest;
                    mensaje = ex.Message;
                    _logger.LogWarning("Argumento inválido en {Metodo} {Ruta}: {Mensaje}", metodo, ruta, ex.Message);
                    break;

                case InvalidOperationException ex:
                    statusCode = HttpStatusCode.BadRequest;
                    mensaje = ex.Message;
                    _logger.LogWarning("Operación inválida en {Metodo} {Ruta}: {Mensaje}", metodo, ruta, ex.Message);
                    break;

                default:
                    statusCode = HttpStatusCode.InternalServerError;
                    _logger.LogError(exception, "Error no controlado capturado en ExceptionMiddleware en {Metodo} {Ruta}: {Mensaje}", metodo, ruta, exception.Message);
                    break;
            }

            context.Response.StatusCode = (int)statusCode;

            var errorResponse = new ErrorResponseDto
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
