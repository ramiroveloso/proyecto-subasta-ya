using System.ComponentModel.DataAnnotations;
using PROYECTO_SUBASTA.Application.Validators;

namespace PROYECTO_SUBASTA.Application.DTOs
{
    /// <summary>
    /// DTO de entrada para registrar una nueva oferta con soporte de Concurrencia Optimista.
    /// </summary>
    public class RegistrarPujaDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "El identificador de usuario es obligatorio.")]
        public int UsuarioId { get; set; }

        [MonedaPrecision(ErrorMessage = "El monto a ofertar debe ser mayor a cero, no superar 100.000.000 y contener como máximo 2 decimales.")]
        public decimal Monto { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "El número de versión de concurrencia debe ser mayor a cero.")]
        public int Version { get; set; }
    }
}
