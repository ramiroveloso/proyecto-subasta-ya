using System;
using Xunit;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Domain.Exceptions;

namespace PROYECTO_SUBASTA.Domain.Tests
{
    public class BilleteraTests
    {
        [Fact]
        public void LiberarSaldo_ConMontoMayorAlRetenido_LanzaDominioException()
        {
            // Arrange: Billetera con $10.000 disponibles y $5.000 retenidos
            var billetera = new Billetera
            {
                Id = 1,
                UsuarioId = 10,
                SaldoTotal = 15000m,
                SaldoDisponible = 10000m,
                SaldoRetenido = 5000m,
                Version = 1
            };

            // Act & Assert: Intentar liberar $5.001 (más de lo retenido) debe lanzar DominioException
            var ex = Assert.Throws<DominioException>(() => billetera.LiberarSaldo(5001m));
            Assert.Contains("No existe retención previa suficiente", ex.Message);
        }

        [Fact]
        public void LiberarSaldo_ConMontoCeroONegativo_LanzaDominioException()
        {
            // Arrange
            var billetera = new Billetera
            {
                Id = 1,
                UsuarioId = 10,
                SaldoTotal = 10000m,
                SaldoDisponible = 5000m,
                SaldoRetenido = 5000m,
                Version = 1
            };

            // Act & Assert
            Assert.Throws<DominioException>(() => billetera.LiberarSaldo(0m));
            Assert.Throws<DominioException>(() => billetera.LiberarSaldo(-100m));
        }

        [Fact]
        public void LiberarSaldo_ConMontoValido_ActualizaSaldosEIncrementaVersion()
        {
            // Arrange: Retenido = $5.000, Disponible = $10.000, Version = 2
            var billetera = new Billetera
            {
                Id = 1,
                UsuarioId = 10,
                SaldoTotal = 15000m,
                SaldoDisponible = 10000m,
                SaldoRetenido = 5000m,
                Version = 2
            };

            // Act: Liberar $2.000
            billetera.LiberarSaldo(2000m);

            // Assert: Retenido = $3.000, Disponible = $12.000, Version = 3
            Assert.Equal(3000m, billetera.SaldoRetenido);
            Assert.Equal(12000m, billetera.SaldoDisponible);
            Assert.Equal(15000m, billetera.SaldoTotal);
            Assert.Equal(3, billetera.Version);
        }

        [Fact]
        public void RetenerSaldo_ConFondosInsuficientes_LanzaDominioException()
        {
            // Arrange: Disponible = $1.000
            var billetera = new Billetera
            {
                Id = 1,
                UsuarioId = 10,
                SaldoTotal = 1000m,
                SaldoDisponible = 1000m,
                SaldoRetenido = 0m,
                Version = 1
            };

            // Act & Assert: Intentar retener $2.000
            var ex = Assert.Throws<DominioException>(() => billetera.RetenerSaldo(2000m));
            Assert.Contains("Fondos insuficientes", ex.Message);
        }

        [Fact]
        public void RetenerSaldo_ConMontoValido_ActualizaSaldosEIncrementaVersion()
        {
            // Arrange
            var billetera = new Billetera
            {
                Id = 1,
                UsuarioId = 10,
                SaldoTotal = 10000m,
                SaldoDisponible = 10000m,
                SaldoRetenido = 0m,
                Version = 1
            };

            // Act
            billetera.RetenerSaldo(3000m);

            // Assert
            Assert.Equal(7000m, billetera.SaldoDisponible);
            Assert.Equal(3000m, billetera.SaldoRetenido);
            Assert.Equal(10000m, billetera.SaldoTotal);
            Assert.Equal(2, billetera.Version);
        }

        [Fact]
        public void CargarSaldo_IncrementaVersionYSaldos()
        {
            // Arrange
            var billetera = new Billetera
            {
                Id = 1,
                UsuarioId = 10,
                SaldoTotal = 5000m,
                SaldoDisponible = 5000m,
                SaldoRetenido = 0m,
                Version = 1
            };

            // Act
            billetera.CargarSaldo(2500m);

            // Assert
            Assert.Equal(7500m, billetera.SaldoDisponible);
            Assert.Equal(7500m, billetera.SaldoTotal);
            Assert.Equal(2, billetera.Version);
        }
    }
}
