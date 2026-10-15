using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PROYECTO_SUBASTA.Application.Validators;

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
        public int? UsuarioId { get; set; }

        [MonedaPrecision(ErrorMessage = "El monto a cargar debe ser mayor a cero, no superar 100.000.000 y contener como máximo 2 decimales.")]
        public decimal Monto { get; set; }
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