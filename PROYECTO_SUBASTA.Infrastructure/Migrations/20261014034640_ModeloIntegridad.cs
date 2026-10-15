using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PROYECTO_SUBASTA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ModeloIntegridad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                column: "Version",
                value: 1);

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

            migrationBuilder.UpdateData(
                table: "Pujas",
                keyColumn: "Id",
                keyValue: 1,
                column: "FechaCreacion",
                value: new DateTime(2026, 10, 14, 3, 31, 39, 196, DateTimeKind.Utc).AddTicks(6500));

            migrationBuilder.UpdateData(
                table: "Pujas",
                keyColumn: "Id",
                keyValue: 2,
                column: "FechaCreacion",
                value: new DateTime(2026, 10, 14, 3, 41, 39, 196, DateTimeKind.Utc).AddTicks(6501));

            migrationBuilder.InsertData(
                table: "Pujas",
                columns: new[] { "Id", "FechaCreacion", "Monto", "SubastaId", "UsuarioId" },
                values: new object[] { 3, new DateTime(2026, 10, 13, 3, 46, 39, 196, DateTimeKind.Utc).AddTicks(6503), 25000.00m, 4, 2 });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaFin", "FechaInicio", "Version" },
                values: new object[] { new DateTime(2026, 10, 14, 4, 11, 39, 196, DateTimeKind.Utc).AddTicks(6421), new DateTime(2026, 10, 14, 3, 26, 39, 196, DateTimeKind.Utc).AddTicks(6414), 3u });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "FechaFin", "FechaInicio", "Version" },
                values: new object[] { new DateTime(2026, 10, 14, 3, 48, 9, 196, DateTimeKind.Utc).AddTicks(6424), new DateTime(2026, 10, 14, 2, 48, 39, 196, DateTimeKind.Utc).AddTicks(6423), 1u });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "FechaFin", "FechaInicio", "Version" },
                values: new object[] { new DateTime(2026, 10, 16, 3, 46, 39, 196, DateTimeKind.Utc).AddTicks(6429), new DateTime(2026, 10, 15, 3, 46, 39, 196, DateTimeKind.Utc).AddTicks(6426), 1u });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "FechaFin", "FechaInicio", "GanadorId", "PrecioFinal", "Version" },
                values: new object[] { new DateTime(2026, 10, 14, 1, 46, 39, 196, DateTimeKind.Utc).AddTicks(6433), new DateTime(2026, 10, 12, 3, 46, 39, 196, DateTimeKind.Utc).AddTicks(6431), 2, 25000.00m, 2u });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "FechaFin", "FechaInicio", "Version" },
                values: new object[] { new DateTime(2026, 10, 13, 3, 46, 39, 196, DateTimeKind.Utc).AddTicks(6438), new DateTime(2026, 10, 11, 3, 46, 39, 196, DateTimeKind.Utc).AddTicks(6437), 1u });

            migrationBuilder.UpdateData(
                table: "TransactionLedgers",
                keyColumn: "Id",
                keyValue: 1,
                column: "Fecha",
                value: new DateTime(2026, 10, 13, 3, 46, 39, 196, DateTimeKind.Utc).AddTicks(6524));

            migrationBuilder.UpdateData(
                table: "TransactionLedgers",
                keyColumn: "Id",
                keyValue: 2,
                column: "Fecha",
                value: new DateTime(2026, 10, 14, 3, 41, 39, 196, DateTimeKind.Utc).AddTicks(6525));

            migrationBuilder.InsertData(
                table: "TransactionLedgers",
                columns: new[] { "Id", "BilleteraId", "Fecha", "Monto", "SubastaId", "Tipo" },
                values: new object[,]
                {
                    { 3, 3, new DateTime(2026, 10, 13, 3, 46, 39, 196, DateTimeKind.Utc).AddTicks(6527), 200000.00m, null, 0 },
                    { 4, 3, new DateTime(2026, 10, 14, 3, 31, 39, 196, DateTimeKind.Utc).AddTicks(6528), 35000.00m, 1, 1 },
                    { 5, 3, new DateTime(2026, 10, 14, 3, 41, 39, 196, DateTimeKind.Utc).AddTicks(6530), 35000.00m, 1, 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLedgers_SubastaId",
                table: "TransactionLedgers",
                column: "SubastaId");

            migrationBuilder.CreateIndex(
                name: "IX_Subastas_GanadorId",
                table: "Subastas",
                column: "GanadorId");

            migrationBuilder.CreateIndex(
                name: "IX_LogsAuditoria_UsuarioId",
                table: "LogsAuditoria",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_LogsAuditoria_Usuarios_UsuarioId",
                table: "LogsAuditoria",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

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
                name: "FK_Subastas_Usuarios_GanadorId",
                table: "Subastas");

            migrationBuilder.DropForeignKey(
                name: "FK_Subastas_Usuarios_VendedorId",
                table: "Subastas");

            migrationBuilder.DropForeignKey(
                name: "FK_TransactionLedgers_Subastas_SubastaId",
                table: "TransactionLedgers");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_TransactionLedgers_SubastaId",
                table: "TransactionLedgers");

            migrationBuilder.DropIndex(
                name: "IX_Subastas_GanadorId",
                table: "Subastas");

            migrationBuilder.DropIndex(
                name: "IX_LogsAuditoria_UsuarioId",
                table: "LogsAuditoria");

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
                column: "Version",
                value: 0);

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
                column: "Fecha",
                value: new DateTime(2026, 9, 14, 2, 11, 57, 740, DateTimeKind.Utc).AddTicks(9334));

            migrationBuilder.UpdateData(
                table: "TransactionLedgers",
                keyColumn: "Id",
                keyValue: 2,
                column: "Fecha",
                value: new DateTime(2026, 9, 15, 2, 6, 57, 740, DateTimeKind.Utc).AddTicks(9336));

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
