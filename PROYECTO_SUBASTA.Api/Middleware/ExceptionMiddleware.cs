using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PROYECTO_SUBASTA.Application.Exceptions;

namespace PROYECTO_SUBASTA.Api.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json";
                var (statusCode, message) = MapExceptionToResponse(ex);
                context.Response.StatusCode = (int)statusCode;

                var payload = new { error = message };
                var json = JsonSerializer.Serialize(payload);
                await context.Response.WriteAsync(json);
            }
        }

        private static (HttpStatusCode, string) MapExceptionToResponse(Exception ex)
        {
            if (ex is ReglaNegocioException) return (HttpStatusCode.BadRequest, ex.Message);
            if (ex is RecursoNoEncontradoException) return (HttpStatusCode.NotFound, ex.Message);
            if (ex is DbUpdateConcurrencyException) return (HttpStatusCode.Conflict, "Conflicto de concurrencia. Vuelva a intentar.");
            if (ex is ArgumentException || ex is InvalidOperationException) return (HttpStatusCode.BadRequest, ex.Message);

            return (HttpStatusCode.InternalServerError, "Error interno del servidor.");
        }
    }
}
