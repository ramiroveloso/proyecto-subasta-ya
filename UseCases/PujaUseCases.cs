using PROYECTO_SUBASTA.Entities;
using PROYECTO_SUBASTA.Repositories;

namespace PROYECTO_SUBASTA.UseCases
{
    public class PujaUseCases
    {
        private readonly IPujaRepository _pujaRepository;
        private readonly ISubastaRepository _subastaRepository;
        private readonly IBilleteraService _billeteraService;

        public PujaUseCases(
            IPujaRepository pujaRepository,
            ISubastaRepository subastaRepository,
            IBilleteraService billeteraService)
        {
            _pujaRepository = pujaRepository;
            _subastaRepository = subastaRepository;
            _billeteraService = billeteraService;
        }

        public async Task<Puja> RegistrarPujaAsync(int subastaId, int usuarioId, decimal montoOferta)
        {
            // 1. VALIDACIÓN DE ESTADO DE LA SUBASTA
            var subasta = await _subastaRepository.ObtenerPorIdAsync(subastaId);

            if (subasta == null)
                throw new ArgumentException("La subasta no existe.");

            // Validar que esté en ventana de tiempo válida (y opcionalmente, podrías validar subasta.Estado == "ACTIVA")
            if (subasta.FechaFin < DateTime.UtcNow)
                throw new ArgumentException("La subasta ya ha finalizado.");

            // 2. VALIDACIÓN DE INCREMENTO MÍNIMO USANDO TU MODELO
            // Determinamos el precio a superar: si ya hay pujas usamos PrecioFinal, sino partimos del PrecioBase
            decimal precioActual = subasta.PrecioFinal ?? subasta.PrecioBase;

            if (montoOferta < (precioActual + subasta.IncrementoMinimo))
                throw new ArgumentException($"La oferta no supera el incremento mínimo. Debe ser de al menos {precioActual + subasta.IncrementoMinimo:C}.");

            // 3. VALIDACIÓN FINANCIERA (Usando el servicio de tu compañero)
            var billetera = await _billeteraService.ObtenerBilleteraPorUsuarioAsync(usuarioId);

            if (billetera == null)
                throw new ArgumentException("El usuario no tiene una billetera asociada.");

            if (billetera.SaldoDisponible < montoOferta)
                throw new ArgumentException("Saldo insuficiente en la billetera para realizar esta oferta.");

            // 4. REGLA ANTI-SNIPING
            var tiempoRestante = subasta.FechaFin - DateTime.UtcNow;
            if (tiempoRestante.TotalMinutes < 5)
            {
                subasta.FechaFin = subasta.FechaFin.AddMinutes(5);
            }

            // 5. ACTUALIZAR ESTADO Y RETENER FONDOS
            // Actualizamos la subasta (EF Core verificará la propiedad Version al guardar)
            subasta.PrecioFinal = montoOferta;
            subasta.GanadorId = usuarioId; // Guardamos momentáneamente quién va ganando

            // Movemos el dinero manualmente
            billetera.SaldoDisponible -= montoOferta;
            billetera.SaldoRetenido += montoOferta;

            // 6. CREAR Y PERSISTIR LA PUJA
            var nuevaPuja = new Puja
            {
                SubastaId = subastaId,
                UsuarioId = usuarioId,
                Monto = montoOferta,
                FechaCreacion = DateTime.UtcNow // <-- Propiedad corregida
            };

            await _pujaRepository.CrearAsync(nuevaPuja);
            await _pujaRepository.GuardarCambiosAsync();

            return nuevaPuja;
        }
    }
}