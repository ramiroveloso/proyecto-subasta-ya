namespace PROYECTO_SUBASTA.Application.Commands.Subastas
{
    public class CerrarSubastaCommand
    {
        public int SubastaId { get; set; }
    }

    public class CerrarSubastaResultadoDto
    {
        public int SubastaId { get; set; }
        public string Estado { get; set; } = string.Empty;
        public int? GanadorId { get; set; }
        public decimal? PrecioFinal { get; set; }
        public bool Exitoso { get; set; }
        public string? Mensaje { get; set; }
    }
}
