using System;
using System.Collections.Generic;
using Xunit;
using PROYECTO_SUBASTA.Domain.Entities;
using PROYECTO_SUBASTA.Domain.Exceptions;

namespace PROYECTO_SUBASTA.Domain.Tests
{
    public class SubastaTests
    {
        [Fact]
        public void ValidarPuedePujar_SubastaVencida_LanzaDominioException()
        {
            // Arrange
            var ahora = DateTime.UtcNow;
            var subasta = new Subasta
            {
                Id = 1,
                VendedorId = 1,
                Estado = "ACTIVA",
                PrecioBase = 10000m,
                IncrementoMinimo = 1000m,
                FechaInicio = ahora.AddHours(-2),
                FechaFin = ahora.AddMinutes(-10) // Vencida hace 10 minutos
            };

            // Act & Assert
            var ex = Assert.Throws<DominioException>(() =>
                subasta.ValidarPuedePujar(usuarioId: 2, monto: 15000m, ahoraUtc: ahora));
            Assert.Contains("finalizado su tiempo", ex.Message);
        }

        [Fact]
        public void ValidarPuedePujar_SubastaNoActiva_LanzaDominioException()
        {
            // Arrange
            var ahora = DateTime.UtcNow;
            var subasta = new Subasta
            {
                Id = 1,
                VendedorId = 1,
                Estado = "PROGRAMADA",
                PrecioBase = 10000m,
                IncrementoMinimo = 1000m,
                FechaInicio = ahora.AddHours(5),
                FechaFin = ahora.AddHours(10)
            };

            // Act & Assert
            var ex = Assert.Throws<DominioException>(() =>
                subasta.ValidarPuedePujar(usuarioId: 2, monto: 15000m, ahoraUtc: ahora));
            Assert.Contains("no está activa", ex.Message);
        }

        [Fact]
        public void ValidarPuedePujar_VendedorPujandoEnSuSubasta_LanzaDominioException()
        {
            // Arrange
            var ahora = DateTime.UtcNow;
            var subasta = new Subasta
            {
                Id = 1,
                VendedorId = 1,
                Estado = "ACTIVA",
                PrecioBase = 10000m,
                IncrementoMinimo = 1000m,
                FechaInicio = ahora.AddMinutes(-30),
                FechaFin = ahora.AddMinutes(30)
            };

            // Act & Assert: Vendedor (Id=1) intenta pujar
            var ex = Assert.Throws<DominioException>(() =>
                subasta.ValidarPuedePujar(usuarioId: 1, monto: 15000m, ahoraUtc: ahora));
            Assert.Contains("vendedor no tiene permitido pujar", ex.Message);
        }

        [Fact]
        public void ValidarPuedePujar_PostorLiderSobrepujandose_LanzaDominioException()
        {
            // Arrange
            var ahora = DateTime.UtcNow;
            var subasta = new Subasta
            {
                Id = 1,
                VendedorId = 1,
                Estado = "ACTIVA",
                PrecioBase = 10000m,
                IncrementoMinimo = 1000m,
                FechaInicio = ahora.AddMinutes(-30),
                FechaFin = ahora.AddMinutes(30),
                Pujas = new List<Puja>
                {
                    new Puja { Id = 1, SubastaId = 1, UsuarioId = 2, Monto = 12000m, FechaCreacion = ahora.AddMinutes(-10) }
                }
            };

            // Act & Assert: Usuario 2 ya es el postor líder
            var ex = Assert.Throws<DominioException>(() =>
                subasta.ValidarPuedePujar(usuarioId: 2, monto: 15000m, ahoraUtc: ahora));
            Assert.Contains("postor líder actual no puede realizar una sobrepuja", ex.Message);
        }

        [Fact]
        public void ValidarPuedePujar_PrimeraPujaMenorAPrecioBaseMasIncrementoMinimo_LanzaDominioException()
        {
            // Arrange: Subasta sin ofertas previas, Base = $10.000, Incremento = $1.000 (Mínimo primera oferta = $11.000)
            var ahora = DateTime.UtcNow;
            var subasta = new Subasta
            {
                Id = 1,
                VendedorId = 1,
                Estado = "ACTIVA",
                PrecioBase = 10000m,
                IncrementoMinimo = 1000m,
                FechaInicio = ahora.AddMinutes(-30),
                FechaFin = ahora.AddMinutes(30)
            };

            // Act & Assert: Oferta de $10.500 no alcanza Base + Incremento
            var ex = Assert.Throws<DominioException>(() =>
                subasta.ValidarPuedePujar(usuarioId: 2, monto: 10500m, ahoraUtc: ahora));
            Assert.Contains("La primera puja debe ser de al menos", ex.Message);
        }

        [Fact]
        public void ValidarPuedePujar_PujaSubsecuenteMenorAMayorPujaMasIncrementoMinimo_LanzaDominioException()
        {
            // Arrange: Líder = $20.000, Incremento = $2.000 (Mínimo requerido = $22.000)
            var ahora = DateTime.UtcNow;
            var subasta = new Subasta
            {
                Id = 1,
                VendedorId = 1,
                Estado = "ACTIVA",
                PrecioBase = 10000m,
                IncrementoMinimo = 2000m,
                FechaInicio = ahora.AddMinutes(-30),
                FechaFin = ahora.AddMinutes(30),
                Pujas = new List<Puja>
                {
                    new Puja { Id = 1, SubastaId = 1, UsuarioId = 2, Monto = 20000m, FechaCreacion = ahora.AddMinutes(-5) }
                }
            };

            // Act & Assert: Usuario 3 oferta $21.000 (supera al líder pero no por el incremento mínimo de $2.000)
            var ex = Assert.Throws<DominioException>(() =>
                subasta.ValidarPuedePujar(usuarioId: 3, monto: 21000m, ahoraUtc: ahora));
            Assert.Contains("debe superar la puja líder actual", ex.Message);
        }

        [Fact]
        public void ValidarPuedePujar_SaldoInsuficiente_LanzaDominioException()
        {
            // Arrange
            var ahora = DateTime.UtcNow;
            var subasta = new Subasta
            {
                Id = 1,
                VendedorId = 1,
                Estado = "ACTIVA",
                PrecioBase = 10000m,
                IncrementoMinimo = 1000m,
                FechaInicio = ahora.AddMinutes(-30),
                FechaFin = ahora.AddMinutes(30)
            };

            // Act & Assert: Postor con $5.000 disponibles intenta ofertar $12.000
            var ex = Assert.Throws<DominioException>(() =>
                subasta.ValidarPuedePujar(usuarioId: 2, monto: 12000m, ahoraUtc: ahora, saldoDisponiblePostor: 5000m));
            Assert.Contains("Saldo disponible insuficiente", ex.Message);
        }

        [Fact]
        public void RegistrarPuja_PujaValidaEnUltimoMinuto_ExtiendeFechaFinPorAntiSnipingEIncrementaVersion()
        {
            // Arrange: Cierra en 45 segundos (dentro del umbral de 60s)
            var ahora = DateTime.UtcNow;
            var fechaFinOriginal = ahora.AddSeconds(45);
            var subasta = new Subasta
            {
                Id = 1,
                VendedorId = 1,
                Estado = "ACTIVA",
                PrecioBase = 10000m,
                IncrementoMinimo = 1000m,
                FechaInicio = ahora.AddMinutes(-30),
                FechaFin = fechaFinOriginal,
                Version = 3
            };

            // Act: Registrar puja de $12.000 con umbral de 60s y extensión de 60s
            var resultado = subasta.RegistrarPuja(
                usuarioId: 2,
                monto: 12000m,
                ahoraUtc: ahora,
                saldoDisponiblePostor: 50000m,
                antiSnipingUmbralSegundos: 60,
                antiSnipingExtensionSegundos: 60
            );

            // Assert
            Assert.True(resultado.AntiSnipingActivado);
            Assert.Equal(fechaFinOriginal.AddSeconds(60), subasta.FechaFin);
            Assert.Equal((uint)4, subasta.Version);
            Assert.Single(subasta.Pujas);
            Assert.Equal(12000m, resultado.NuevaPuja.Monto);
        }

        [Fact]
        public void RegistrarPuja_PujaValidaFueraDeUmbral_NoExtiendeFechaFinEIncrementaVersion()
        {
            // Arrange: Cierra en 15 minutos (lejos del umbral de 60s)
            var ahora = DateTime.UtcNow;
            var fechaFinOriginal = ahora.AddMinutes(15);
            var subasta = new Subasta
            {
                Id = 1,
                VendedorId = 1,
                Estado = "ACTIVA",
                PrecioBase = 10000m,
                IncrementoMinimo = 1000m,
                FechaInicio = ahora.AddMinutes(-30),
                FechaFin = fechaFinOriginal,
                Version = 1
            };

            // Act
            var resultado = subasta.RegistrarPuja(
                usuarioId: 2,
                monto: 15000m,
                ahoraUtc: ahora,
                saldoDisponiblePostor: 50000m,
                antiSnipingUmbralSegundos: 60,
                antiSnipingExtensionSegundos: 60
            );

            // Assert
            Assert.False(resultado.AntiSnipingActivado);
            Assert.Equal(fechaFinOriginal, subasta.FechaFin);
            Assert.Equal((uint)2, subasta.Version);
            Assert.Single(subasta.Pujas);
        }
    }
}
