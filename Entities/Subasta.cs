namespace PROYECTO_SUBASTA.Entities
{
    public class Subasta
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string UrlImagen { get; set; } = string.Empty;
        public decimal PrecioBase { get; set; }
        public decimal IncrementoMinimo { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public string Estado { get; set; } = "PROGRAMADA"; // PROGRAMADA, ACTIVA, FINALIZADA, DESIERTA
        public int CategoriaId { get; set; }
        public int VendedorId { get; set; }
        public int? GanadorId { get; set; }
        public decimal? PrecioFinal { get; set; }

        // Control de Concurrencia Optimista (Optimistic Locking)
        public uint Version { get; set; }

        // Relaciones de navegación
        public Categoria? Categoria { get; set; }
        public Usuario? Vendedor { get; set; }
        public ICollection<Puja> Pujas { get; set; } = new List<Puja>();
    }
}