using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PROYECTO_SUBASTA.Application.DTOs
{
    public class RegistrarPujaDto
    {
        public int UsuarioId { get; set; }
        public decimal Monto { get; set; }
        public int Version { get; set; }
    }
}
