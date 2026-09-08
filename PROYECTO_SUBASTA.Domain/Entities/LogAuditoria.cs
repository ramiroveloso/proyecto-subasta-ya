namespace PROYECTO_SUBASTA.Domain.Entities
{
    public class LogAuditoria
    {
        public int Id { get; set; }
        public int? UsuarioId { get; set; }
        public string Accion { get; set; } = string.Empty;
        public string Detalle { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    }
}
