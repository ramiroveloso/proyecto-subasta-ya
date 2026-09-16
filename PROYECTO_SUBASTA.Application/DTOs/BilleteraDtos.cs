using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PROYECTO_SUBASTA.Application.DTOs
{
    /// <summary>
    /// DTO de lectura para la consulta de métricas de billetera (Saldo Total, Retenido y Disponible).
    /// </summary>
    public class BilleteraResponseDto
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public decimal SaldoTotal { get; set; }
        public decimal SaldoRetenido { get; set; }
        public decimal SaldoDisponible { get; set; }
        public int Version { get; set; }
        public List<MovimientoLedgerDto> Movimientos { get; set; } = new();
    }

    /// <summary>
    /// DTO de entrada para la carga de saldo simulado.
    /// </summary>
    public class CargarSaldoDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Debe especificar un usuario válido.")]
        public int UsuarioId { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "El monto a cargar debe ser mayor a cero.")]
        public decimal Monto { get; set; }
    }

    /// <summary>
    /// DTO de entrada para operaciones de garantía en Escrow (Retención / Liberación).
    /// </summary>
    public class OperacionFondosDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Debe especificar un usuario válido.")]
        public int UsuarioId { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser superior a cero.")]
        public decimal Monto { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Debe vincular un ID de subasta válido.")]
        public int SubastaId { get; set; }
    }

    /// <summary>
    /// DTO de lectura para cada asiento del libro diario contable (TransactionLedger).
    /// </summary>
    public class MovimientoLedgerDto
    {
        public int Id { get; set; }
        public int BilleteraId { get; set; }
        public string TipoTransaccion { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public DateTime Fecha { get; set; }
        public int? SubastaId { get; set; }
        public string Concepto { get; set; } = string.Empty;
    }
}