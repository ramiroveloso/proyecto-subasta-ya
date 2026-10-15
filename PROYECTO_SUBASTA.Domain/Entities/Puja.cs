using System;
using PROYECTO_SUBASTA.Domain.Exceptions;

namespace PROYECTO_SUBASTA.Domain.Entities
{
    public class Puja
    {
        private decimal _monto;

        public int Id { get; set; }
        public int SubastaId { get; set; }
        public int UsuarioId { get; set; }

        public decimal Monto
        {
            get => _monto;
            set
            {
                ValidarMonto(value);
                _monto = value;
            }
        }

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        // Relaciones de navegación
        public Subasta? Subasta { get; set; }
        public Usuario? Usuario { get; set; }

        public static void ValidarMonto(decimal monto, decimal limiteMaximo = 100_000_000m)
        {
            if (monto <= 0)
                throw new DominioException("El monto de la puja debe ser mayor a cero.");

            if (decimal.Round(monto, 2) != monto)
                throw new DominioException("El monto de la puja no puede tener más de dos decimales.");

            if (monto > limiteMaximo)
                throw new DominioException($"El monto de la puja no puede superar el límite permitido de ${limiteMaximo:N2}.");
        }
    }
}
