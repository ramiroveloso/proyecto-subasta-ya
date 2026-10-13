using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROYECTO_SUBASTA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarVersionASubasta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                columns: new[] { "FechaFin", "FechaInicio" },
                values: new object[] { new DateTime(2026, 9, 15, 2, 36, 57, 740, DateTimeKind.Utc).AddTicks(9267), new DateTime(2026, 9, 15, 1, 51, 57, 740, DateTimeKind.Utc).AddTicks(9262) });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "FechaFin", "FechaInicio" },
                values: new object[] { new DateTime(2026, 9, 15, 2, 13, 27, 740, DateTimeKind.Utc).AddTicks(9271), new DateTime(2026, 9, 15, 1, 13, 57, 740, DateTimeKind.Utc).AddTicks(9271) });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "FechaFin", "FechaInicio" },
                values: new object[] { new DateTime(2026, 9, 17, 2, 11, 57, 740, DateTimeKind.Utc).AddTicks(9277), new DateTime(2026, 9, 16, 2, 11, 57, 740, DateTimeKind.Utc).AddTicks(9273) });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "FechaFin", "FechaInicio" },
                values: new object[] { new DateTime(2026, 9, 15, 0, 11, 57, 740, DateTimeKind.Utc).AddTicks(9280), new DateTime(2026, 9, 13, 2, 11, 57, 740, DateTimeKind.Utc).AddTicks(9278) });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "FechaFin", "FechaInicio" },
                values: new object[] { new DateTime(2026, 9, 14, 2, 11, 57, 740, DateTimeKind.Utc).AddTicks(9282), new DateTime(2026, 9, 12, 2, 11, 57, 740, DateTimeKind.Utc).AddTicks(9282) });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Pujas",
                keyColumn: "Id",
                keyValue: 1,
                column: "FechaCreacion",
                value: new DateTime(2026, 9, 11, 3, 50, 40, 691, DateTimeKind.Utc).AddTicks(1139));

            migrationBuilder.UpdateData(
                table: "Pujas",
                keyColumn: "Id",
                keyValue: 2,
                column: "FechaCreacion",
                value: new DateTime(2026, 9, 11, 4, 0, 40, 691, DateTimeKind.Utc).AddTicks(1141));

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaFin", "FechaInicio" },
                values: new object[] { new DateTime(2026, 9, 11, 4, 30, 40, 691, DateTimeKind.Utc).AddTicks(1036), new DateTime(2026, 9, 11, 3, 45, 40, 691, DateTimeKind.Utc).AddTicks(1031) });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "FechaFin", "FechaInicio" },
                values: new object[] { new DateTime(2026, 9, 11, 4, 7, 10, 691, DateTimeKind.Utc).AddTicks(1039), new DateTime(2026, 9, 11, 3, 7, 40, 691, DateTimeKind.Utc).AddTicks(1039) });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "FechaFin", "FechaInicio" },
                values: new object[] { new DateTime(2026, 9, 13, 4, 5, 40, 691, DateTimeKind.Utc).AddTicks(1045), new DateTime(2026, 9, 12, 4, 5, 40, 691, DateTimeKind.Utc).AddTicks(1041) });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "FechaFin", "FechaInicio" },
                values: new object[] { new DateTime(2026, 9, 11, 2, 5, 40, 691, DateTimeKind.Utc).AddTicks(1050), new DateTime(2026, 9, 9, 4, 5, 40, 691, DateTimeKind.Utc).AddTicks(1047) });

            migrationBuilder.UpdateData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "FechaFin", "FechaInicio" },
                values: new object[] { new DateTime(2026, 9, 10, 4, 5, 40, 691, DateTimeKind.Utc).AddTicks(1053), new DateTime(2026, 9, 8, 4, 5, 40, 691, DateTimeKind.Utc).AddTicks(1052) });

            migrationBuilder.UpdateData(
                table: "TransactionLedgers",
                keyColumn: "Id",
                keyValue: 1,
                column: "Fecha",
                value: new DateTime(2026, 9, 10, 4, 5, 40, 691, DateTimeKind.Utc).AddTicks(1166));

            migrationBuilder.UpdateData(
                table: "TransactionLedgers",
                keyColumn: "Id",
                keyValue: 2,
                column: "Fecha",
                value: new DateTime(2026, 9, 11, 4, 0, 40, 691, DateTimeKind.Utc).AddTicks(1168));
        }
    }
}
