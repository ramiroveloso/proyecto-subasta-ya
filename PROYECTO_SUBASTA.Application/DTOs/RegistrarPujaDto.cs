using System.ComponentModel.DataAnnotations;

namespace PROYECTO_SUBASTA.Application.DTOs
{
    /// <summary>
    /// DTO de entrada para registrar una nueva oferta con soporte de Concurrencia Optimista.
    /// </summary>
    public class RegistrarPujaDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "El identificador de usuario es obligatorio.")]
        public int UsuarioId { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "El monto a ofertar debe ser un valor positivo.")]
        public decimal Monto { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "El número de versión de concurrencia es inválido.")]
        public int Version { get; set; }
    }
}
