using System;
using PROYECTO_SUBASTA.Application.DTOs;

namespace PROYECTO_SUBASTA.Application.Commands.Subastas
{
    public class RegistrarPujaCommand
    {
        public int SubastaId { get; set; }
        public int UsuarioId { get; set; }
        public decimal Monto { get; set; }
        public int VersionCliente { get; set; }
    }

    public class RegistrarPujaResponseDto
    {
        public string Mensaje { get; set; } = string.Empty;
        public uint Version { get; set; }
        public DateTime FechaFin { get; set; }
        public bool AntiSnipingActivado { get; set; }
        public PujaItemDto Puja { get; set; } = null!;
    }
}
