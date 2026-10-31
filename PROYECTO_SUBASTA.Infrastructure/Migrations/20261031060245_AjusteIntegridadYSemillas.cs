using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PROYECTO_SUBASTA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AjusteIntegridadYSemillas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pujas_Usuarios_UsuarioId",
                table: "Pujas");

            migrationBuilder.DropForeignKey(
                name: "FK_Subastas_Usuarios_VendedorId",
                table: "Subastas");

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioFinal",
                table: "Subastas",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioBase",
                table: "Subastas",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AlterColumn<decimal>(
                name: "IncrementoMinimo",
                table: "Subastas",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.UpdateData(
                table: "Billeteras",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "SaldoDisponible", "SaldoTotal", "Version" },
                values: new object[] { 25000.00m, 25000.00m, 1 });

            migrationBuilder.UpdateData(
                table: "Billeteras",
                keyColumn: "Id",
                keyValue: 2,
                column: "Version",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Billeteras",
                keyColumn: "Id",
                keyValue: 3,
                column: "Version",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Billeteras",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "SaldoDisponible", "SaldoTotal", "Version" },
                values: new object[] { 0.00m, 0.00m, 1 });

            migrationBuilder.InsertData(
                table: "LogsAuditoria",
                columns: new[] { "Id", "Accion", "Detalle", "FechaRegistro", "UsuarioId" },
                values: new object[,]
                {
                    { 1, "CAMBIO_ESTADO_SUBASTA", "Subasta #4 ('Campera de Cuero Vintage') finalizada y adjudicada por Background Worker. Ganador: Usuario #2 (Comprador Líder) con oferta de $25.000,00.", new DateTime(2026, 1, 1, 10, 0, 0, 0, DateTimeKind.Utc), 2 },
                    { 2, "VENTA_REGISTRADA", "Liquidación final de Subasta #4: Saldo retenido ($25.000,00) de Comprador Líder transferido a Billetera de Vendedor Test.", new DateTime(2026, 1, 1, 10, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 3, "CAMBIO_ESTADO_SUBASTA", "Subasta #5 ('Repuesto Clásico de Vehículo') declarada DESIERTA por el Background Worker al vencer el tiempo sin ofertas.", new DateTime(2025, 12, 31, 12, 0, 0, 0, DateTimeKind.Utc), null }
                });

            migrationBuilder.UpdateData(
                table: "Pujas",
                keyColumn: "Id",
                keyValue: 1,
                column: "FechaCreacion",
                value: new DateTime(2026, 1, 2, 11, 45, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Pujas",
                keyColumn: "Id",
                keyValue: 2,
                column: "FechaCreacion",
                value: new DateTime(2026, 1, 2, 11, 55, 0, 0, DateTimeKind.Utc));

            migrationBuilder.InsertData(
                table: "Pujas",
                columns: new[] { "Id", "FechaCreacion", "Monto", "SubastaId", "UsuarioId" },
                values: new object[] { 3, new DateTime(2025, 12, 31, 12, 0, 0, 0, DateTimeKind.Utc), 25000.00m, 4, 2 });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaFin", "FechaInicio", "Version" },
                values: new object[] { new DateTime(2026, 1, 2, 12, 25, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 2, 11, 40, 0, 0, DateTimeKind.Utc), 3u });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "FechaFin", "FechaInicio", "Version" },
                values: new object[] { new DateTime(2026, 1, 2, 12, 1, 30, 0, DateTimeKind.Utc), new DateTime(2026, 1, 2, 11, 2, 0, 0, DateTimeKind.Utc), 1u });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "FechaFin", "FechaInicio", "Version" },
                values: new object[] { new DateTime(2026, 1, 4, 12, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 3, 12, 0, 0, 0, DateTimeKind.Utc), 1u });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "FechaFin", "FechaInicio", "GanadorId", "PrecioFinal", "Version" },
                values: new object[] { new DateTime(2026, 1, 1, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 12, 30, 12, 0, 0, 0, DateTimeKind.Utc), 2, 25000.00m, 2u });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "FechaFin", "FechaInicio", "Version" },
                values: new object[] { new DateTime(2025, 12, 31, 12, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 12, 29, 12, 0, 0, 0, DateTimeKind.Utc), 1u });

            migrationBuilder.UpdateData(
                table: "TransactionLedgers",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "BilleteraId", "Fecha", "Monto", "SubastaId", "Tipo" },
                values: new object[] { 1, new DateTime(2026, 1, 1, 11, 0, 0, 0, DateTimeKind.Utc), 25000.00m, 4, 4 });

            migrationBuilder.UpdateData(
                table: "TransactionLedgers",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Fecha", "Monto", "SubastaId", "Tipo" },
                values: new object[] { new DateTime(2025, 12, 31, 12, 0, 0, 0, DateTimeKind.Utc), 175000.00m, null, 0 });

            migrationBuilder.InsertData(
                table: "TransactionLedgers",
                columns: new[] { "Id", "BilleteraId", "Fecha", "Monto", "SubastaId", "Tipo" },
                values: new object[,]
                {
                    { 3, 2, new DateTime(2025, 12, 31, 12, 5, 0, 0, DateTimeKind.Utc), 25000.00m, 4, 1 },
                    { 4, 2, new DateTime(2026, 1, 1, 11, 0, 0, 0, DateTimeKind.Utc), 25000.00m, 4, 3 },
                    { 5, 2, new DateTime(2026, 1, 2, 11, 55, 0, 0, DateTimeKind.Utc), 45000.00m, 1, 1 },
                    { 6, 3, new DateTime(2025, 12, 31, 12, 0, 0, 0, DateTimeKind.Utc), 200000.00m, null, 0 },
                    { 7, 3, new DateTime(2026, 1, 2, 11, 45, 0, 0, DateTimeKind.Utc), 35000.00m, 1, 1 },
                    { 8, 3, new DateTime(2026, 1, 2, 11, 55, 0, 0, DateTimeKind.Utc), 35000.00m, 1, 2 }
                });

            migrationBuilder.UpdateData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 1,
                column: "FechaRegistro",
                value: new DateTime(2026, 1, 1, 12, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 2,
                column: "FechaRegistro",
                value: new DateTime(2026, 1, 1, 12, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 3,
                column: "FechaRegistro",
                value: new DateTime(2026, 1, 1, 12, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 4,
                column: "FechaRegistro",
                value: new DateTime(2026, 1, 1, 12, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLedgers_SubastaId",
                table: "TransactionLedgers",
                column: "SubastaId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TransactionLedger_Monto",
                table: "TransactionLedgers",
                sql: "Monto > 0");

            migrationBuilder.CreateIndex(
                name: "IX_Subastas_GanadorId",
                table: "Subastas",
                column: "GanadorId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subasta_Fechas",
                table: "Subastas",
                sql: "FechaFin > FechaInicio");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subasta_IncrementoMinimo",
                table: "Subastas",
                sql: "IncrementoMinimo > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subasta_PrecioBase",
                table: "Subastas",
                sql: "PrecioBase > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Puja_Monto",
                table: "Pujas",
                sql: "Monto > 0");

            migrationBuilder.CreateIndex(
                name: "IX_LogsAuditoria_UsuarioId",
                table: "LogsAuditoria",
                column: "UsuarioId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Billetera_SaldoDisponible",
                table: "Billeteras",
                sql: "SaldoDisponible >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Billetera_SaldoRetenido",
                table: "Billeteras",
                sql: "SaldoRetenido >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Billetera_SaldoTotal",
                table: "Billeteras",
                sql: "SaldoTotal = SaldoRetenido + SaldoDisponible");

            migrationBuilder.AddForeignKey(
                name: "FK_LogsAuditoria_Usuarios_UsuarioId",
                table: "LogsAuditoria",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Pujas_Usuarios_UsuarioId",
                table: "Pujas",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subastas_Usuarios_GanadorId",
                table: "Subastas",
                column: "GanadorId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subastas_Usuarios_VendedorId",
                table: "Subastas",
                column: "VendedorId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TransactionLedgers_Subastas_SubastaId",
                table: "TransactionLedgers",
                column: "SubastaId",
                principalTable: "Subastas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LogsAuditoria_Usuarios_UsuarioId",
                table: "LogsAuditoria");

            migrationBuilder.DropForeignKey(
                name: "FK_Pujas_Usuarios_UsuarioId",
                table: "Pujas");

            migrationBuilder.DropForeignKey(
                name: "FK_Subastas_Usuarios_GanadorId",
                table: "Subastas");

            migrationBuilder.DropForeignKey(
                name: "FK_Subastas_Usuarios_VendedorId",
                table: "Subastas");

            migrationBuilder.DropForeignKey(
                name: "FK_TransactionLedgers_Subastas_SubastaId",
                table: "TransactionLedgers");

            migrationBuilder.DropIndex(
                name: "IX_TransactionLedgers_SubastaId",
                table: "TransactionLedgers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TransactionLedger_Monto",
                table: "TransactionLedgers");

            migrationBuilder.DropIndex(
                name: "IX_Subastas_GanadorId",
                table: "Subastas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subasta_Fechas",
                table: "Subastas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subasta_IncrementoMinimo",
                table: "Subastas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subasta_PrecioBase",
                table: "Subastas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Puja_Monto",
                table: "Pujas");

            migrationBuilder.DropIndex(
                name: "IX_LogsAuditoria_UsuarioId",
                table: "LogsAuditoria");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Billetera_SaldoDisponible",
                table: "Billeteras");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Billetera_SaldoRetenido",
                table: "Billeteras");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Billetera_SaldoTotal",
                table: "Billeteras");

            migrationBuilder.DeleteData(
                table: "LogsAuditoria",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "LogsAuditoria",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "LogsAuditoria",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Pujas",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "TransactionLedgers",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "TransactionLedgers",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "TransactionLedgers",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "TransactionLedgers",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "TransactionLedgers",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "TransactionLedgers",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioFinal",
                table: "Subastas",
                type: "decimal(65,30)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioBase",
                table: "Subastas",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "IncrementoMinimo",
                table: "Subastas",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.UpdateData(
                table: "Billeteras",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "SaldoDisponible", "SaldoTotal", "Version" },
                values: new object[] { 0.00m, 0.00m, 0 });

            migrationBuilder.UpdateData(
                table: "Billeteras",
                keyColumn: "Id",
                keyValue: 2,
                column: "Version",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Billeteras",
                keyColumn: "Id",
                keyValue: 3,
                column: "Version",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Billeteras",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "SaldoDisponible", "SaldoTotal", "Version" },
                values: new object[] { 500.00m, 500.00m, 0 });

            migrationBuilder.UpdateData(
                table: "Pujas",
                keyColumn: "Id",
                keyValue: 1,
                column: "FechaCreacion",
                value: new DateTime(2026, 9, 15, 1, 56, 57, 740, DateTimeKind.Utc).AddTicks(9308));

            migrationBuilder.UpdateData(
                table: "Pujas",
                keyColumn: "Id",
                keyValue: 2,
                column: "FechaCreacion",
                value: new DateTime(2026, 9, 15, 2, 6, 57, 740, DateTimeKind.Utc).AddTicks(9310));

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaFin", "FechaInicio", "Version" },
                values: new object[] { new DateTime(2026, 9, 15, 2, 36, 57, 740, DateTimeKind.Utc).AddTicks(9267), new DateTime(2026, 9, 15, 1, 51, 57, 740, DateTimeKind.Utc).AddTicks(9262), 0u });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "FechaFin", "FechaInicio", "Version" },
                values: new object[] { new DateTime(2026, 9, 15, 2, 13, 27, 740, DateTimeKind.Utc).AddTicks(9271), new DateTime(2026, 9, 15, 1, 13, 57, 740, DateTimeKind.Utc).AddTicks(9271), 0u });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "FechaFin", "FechaInicio", "Version" },
                values: new object[] { new DateTime(2026, 9, 17, 2, 11, 57, 740, DateTimeKind.Utc).AddTicks(9277), new DateTime(2026, 9, 16, 2, 11, 57, 740, DateTimeKind.Utc).AddTicks(9273), 0u });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "FechaFin", "FechaInicio", "GanadorId", "PrecioFinal", "Version" },
                values: new object[] { new DateTime(2026, 9, 15, 0, 11, 57, 740, DateTimeKind.Utc).AddTicks(9280), new DateTime(2026, 9, 13, 2, 11, 57, 740, DateTimeKind.Utc).AddTicks(9278), null, null, 0u });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "FechaFin", "FechaInicio", "Version" },
                values: new object[] { new DateTime(2026, 9, 14, 2, 11, 57, 740, DateTimeKind.Utc).AddTicks(9282), new DateTime(2026, 9, 12, 2, 11, 57, 740, DateTimeKind.Utc).AddTicks(9282), 0u });

            migrationBuilder.UpdateData(
                table: "TransactionLedgers",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "BilleteraId", "Fecha", "Monto", "SubastaId", "Tipo" },
                values: new object[] { 2, new DateTime(2026, 9, 14, 2, 11, 57, 740, DateTimeKind.Utc).AddTicks(9334), 150000.00m, null, 0 });

            migrationBuilder.UpdateData(
                table: "TransactionLedgers",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Fecha", "Monto", "SubastaId", "Tipo" },
                values: new object[] { new DateTime(2026, 9, 15, 2, 6, 57, 740, DateTimeKind.Utc).AddTicks(9336), 45000.00m, 1, 1 });

            migrationBuilder.UpdateData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 1,
                column: "FechaRegistro",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 2,
                column: "FechaRegistro",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 3,
                column: "FechaRegistro",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 4,
                column: "FechaRegistro",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddForeignKey(
                name: "FK_Pujas_Usuarios_UsuarioId",
                table: "Pujas",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Subastas_Usuarios_VendedorId",
                table: "Subastas",
                column: "VendedorId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
