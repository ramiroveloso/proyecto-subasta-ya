namespace PROYECTO_SUBASTA.Entities
{
    public class Puja
    {
        public int Id { get; set; }
        public int SubastaId { get; set; }
        public int UsuarioId { get; set; }
        public decimal Monto { get; set; }
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        // Relaciones de navegación
        public Subasta? Subasta { get; set; }
        public Usuario? Usuario { get; set; }
    }
}
