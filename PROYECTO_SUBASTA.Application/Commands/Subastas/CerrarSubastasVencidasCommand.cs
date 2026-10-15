using System;

namespace PROYECTO_SUBASTA.Application.Commands.Subastas
{
    public class CerrarSubastasVencidasCommand
    {
        public DateTime? FechaReferenciaUtc { get; set; }

        public CerrarSubastasVencidasCommand(DateTime? fechaReferenciaUtc = null)
        {
            FechaReferenciaUtc = fechaReferenciaUtc;
        }
    }
}
