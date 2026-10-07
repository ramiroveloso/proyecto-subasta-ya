using System;
using System.Collections.Generic;
using System.Linq;
using PROYECTO_SUBASTA.Domain.Exceptions;

namespace PROYECTO_SUBASTA.Domain.Entities
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

        // ========================================================================
        // MÉTODOS DE NEGOCIO Y DOMINIO RICO (Clean Architecture)
        // ========================================================================

        /// <summary>
        /// Obtiene la puja de mayor monto actualmente registrada en la subasta.
        /// </summary>
        public Puja? ObtenerPujaLider()
        {
            return Pujas?.OrderByDescending(p => p.Monto).FirstOrDefault();
        }

        /// <summary>
        /// Obtiene el monto mínimo admisible para la próxima oferta.
        /// Si no existen pujas, es PrecioBase + IncrementoMinimo.
        /// Si ya existen pujas, es MayorPuja.Monto + IncrementoMinimo.
        /// </summary>
        public decimal ObtenerMontoMinimoProximaPuja()
        {
            var lider = ObtenerPujaLider();
            return lider != null
                ? lider.Monto + IncrementoMinimo
                : PrecioBase + IncrementoMinimo;
        }

        /// <summary>
        /// Valida rigurosamente las reglas de negocio para determinar si un postor puede ofertar.
        /// Lanza DominioException ante cualquier regla no cumplida.
        /// </summary>
        public void ValidarPuedePujar(int usuarioId, decimal monto, DateTime ahoraUtc, decimal? saldoDisponiblePostor = null)
        {
            if (Estado != "ACTIVA")
                throw new DominioException($"No se pueden realizar pujas en una subasta que no está activa (Estado actual: '{Estado}').");

            if (ahoraUtc >= FechaFin)
                throw new DominioException($"La subasta ya ha finalizado su tiempo reglamentario de recepción de ofertas (Fecha fin: {FechaFin:yyyy-MM-dd HH:mm:ss} UTC, Tiempo actual: {ahoraUtc:yyyy-MM-dd HH:mm:ss} UTC).");

            if (usuarioId == VendedorId)
                throw new DominioException("El vendedor no tiene permitido pujar en su propia subasta.");

            var pujaLider = ObtenerPujaLider();
            if (pujaLider != null && pujaLider.UsuarioId == usuarioId)
                throw new DominioException("El postor líder actual no puede realizar una sobrepuja sobre su propia oferta.");

            var minimoRequerido = ObtenerMontoMinimoProximaPuja();
            if (monto < minimoRequerido)
            {
                if (pujaLider == null)
                {
                    throw new DominioException($"La primera puja debe ser de al menos ${minimoRequerido:N2} (Precio base de ${PrecioBase:N2} + incremento mínimo de ${IncrementoMinimo:N2}).");
                }
                else
                {
                    throw new DominioException($"El monto ofertado (${monto:N2}) debe superar la puja líder actual (${pujaLider.Monto:N2}) por al menos el incremento mínimo (${IncrementoMinimo:N2}). Mínimo requerido: ${minimoRequerido:N2}.");
                }
            }

            if (saldoDisponiblePostor.HasValue && saldoDisponiblePostor.Value < monto)
            {
                throw new DominioException($"Saldo disponible insuficiente (${saldoDisponiblePostor.Value:N2}) para respaldar la puja de ${monto:N2} en garantía.");
            }
        }

        /// <summary>
        /// Evalúa la regla Anti-Sniping paramétrica.
        /// Si la puja entra en el umbral previo al cierre (por defecto 60s), extiende la FechaFin.
        /// </summary>
        public bool EvaluarAntiSniping(DateTime ahoraUtc, int umbralSegundos = 60, int extensionSegundos = 60)
        {
            var tiempoRestante = FechaFin - ahoraUtc;
            if (tiempoRestante > TimeSpan.Zero && tiempoRestante.TotalSeconds <= umbralSegundos)
            {
                FechaFin = FechaFin.AddSeconds(extensionSegundos);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Valida y registra una nueva puja en la entidad de forma atómica.
        /// Incrementa la versión de concurrencia y aplica la extensión anti-sniping si corresponde.
        /// </summary>
        public (Puja NuevaPuja, bool AntiSnipingActivado) RegistrarPuja(
            int usuarioId,
            decimal monto,
            DateTime ahoraUtc,
            decimal? saldoDisponiblePostor = null,
            int antiSnipingUmbralSegundos = 60,
            int antiSnipingExtensionSegundos = 60)
        {
            ValidarPuedePujar(usuarioId, monto, ahoraUtc, saldoDisponiblePostor);

            bool antiSnipingActivado = EvaluarAntiSniping(ahoraUtc, antiSnipingUmbralSegundos, antiSnipingExtensionSegundos);

            var nuevaPuja = new Puja
            {
                SubastaId = Id,
                UsuarioId = usuarioId,
                Monto = monto,
                FechaCreacion = ahoraUtc
            };

            if (Pujas == null)
            {
                Pujas = new List<Puja>();
            }

            Pujas.Add(nuevaPuja);
            Version++;

            return (nuevaPuja, antiSnipingActivado);
        }

        /// <summary>
        /// Concluye la subasta con adjudicación y ganador.
        /// </summary>
        public void Finalizar(int ganadorId, decimal precioFinal)
        {
            if (Estado != "ACTIVA" && Estado != "PROGRAMADA")
                throw new DominioException($"No se puede finalizar una subasta en estado '{Estado}'.");

            Estado = "FINALIZADA";
            GanadorId = ganadorId;
            PrecioFinal = precioFinal;
            Version++;
        }

        /// <summary>
        /// Declara desierta la subasta al vencer sin ofertas.
        /// </summary>
        public void DeclararDesierta()
        {
            if (Estado != "ACTIVA" && Estado != "PROGRAMADA")
                throw new DominioException($"No se puede declarar desierta una subasta en estado '{Estado}'.");

            Estado = "DESIERTA";
            GanadorId = null;
            PrecioFinal = null;
            Version++;
        }
    }
}
