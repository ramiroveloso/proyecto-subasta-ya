using System;

namespace PROYECTO_SUBASTA.Application.Commands.Subastas
{
    /// <summary>
    /// Comando de aplicación para la creación de una subasta con campos propios desacoplados de la capa HTTP.
    /// </summary>
    public class CrearSubastaCommand
    {
        public string Titulo { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public string? UrlImagen { get; set; }
        public decimal PrecioBase { get; set; }
        public decimal IncrementoMinimo { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public int CategoriaId { get; set; }
        public int VendedorId { get; set; }
    }
}
