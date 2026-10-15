using System;

namespace PROYECTO_SUBASTA.Application.Commands.Subastas
{
    public class CrearSubastaCommand
    {
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string UrlImagen { get; set; } = string.Empty;
        public decimal PrecioBase { get; set; }
        public decimal IncrementoMinimo { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public int CategoriaId { get; set; }
        public int VendedorId { get; set; }
    }
}
