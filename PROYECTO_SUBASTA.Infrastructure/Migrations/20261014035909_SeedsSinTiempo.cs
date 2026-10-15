using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PROYECTO_SUBASTA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedsSinTiempo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Billeteras",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Billeteras",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Pujas",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Pujas",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Pujas",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "TransactionLedgers",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "TransactionLedgers",
                keyColumn: "Id",
                keyValue: 2);

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
                table: "Billeteras",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Billeteras",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Subastas",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Usuarios",
                columns: new[] { "Id", "Email", "FechaRegistro", "Nombre", "PasswordHash" },
                values: new object[,]
                {
                    { 1, "vendedor@test.com", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Vendedor Test", "hash_vendedor" },
                    { 2, "comprador1@test.com", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Comprador Líder", "hash_c1" },
                    { 3, "comprador2@test.com", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Comprador Habilitado", "hash_c2" },
                    { 4, "sinfondos@test.com", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Usuario Sin Fondos", "hash_sf" }
                });

            migrationBuilder.InsertData(
                table: "Billeteras",
                columns: new[] { "Id", "SaldoDisponible", "SaldoRetenido", "SaldoTotal", "UsuarioId", "Version" },
                values: new object[,]
                {
                    { 1, 0.00m, 0.00m, 0.00m, 1, 1 },
                    { 2, 105000.00m, 45000.00m, 150000.00m, 2, 1 },
                    { 3, 200000.00m, 0.00m, 200000.00m, 3, 1 },
                    { 4, 0.00m, 0.00m, 0.00m, 4, 1 }
                });

            migrationBuilder.InsertData(
                table: "Subastas",
                columns: new[] { "Id", "CategoriaId", "Descripcion", "Estado", "FechaFin", "FechaInicio", "GanadorId", "IncrementoMinimo", "PrecioBase", "PrecioFinal", "Titulo", "UrlImagen", "VendedorId", "Version" },
                values: new object[,]
                {
                    { 1, 1, "GPU de alta gama para diseño y gaming", "ACTIVA", new DateTime(2026, 10, 14, 4, 11, 39, 196, DateTimeKind.Utc).AddTicks(6421), new DateTime(2026, 10, 14, 3, 26, 39, 196, DateTimeKind.Utc).AddTicks(6414), null, 5000.00m, 30000.00m, null, "Placa de Video RTX", "https://images.unsplash.com/photo-1587202372775-e229f172b9d7?w=500", 1, 3u },
                    { 2, 1, "Ideal para estaciones de trabajo", "ACTIVA", new DateTime(2026, 10, 14, 3, 48, 9, 196, DateTimeKind.Utc).AddTicks(6424), new DateTime(2026, 10, 14, 2, 48, 39, 196, DateTimeKind.Utc).AddTicks(6423), null, 5000.00m, 50000.00m, null, "Procesador de Alta Gama", "https://images.unsplash.com/photo-1591799264318-7e6ef8ddb7ea?w=500", 1, 1u },
                    { 3, 2, "Arte y diseño exclusivo", "PROGRAMADA", new DateTime(2026, 10, 16, 3, 46, 39, 196, DateTimeKind.Utc).AddTicks(6429), new DateTime(2026, 10, 15, 3, 46, 39, 196, DateTimeKind.Utc).AddTicks(6426), null, 2000.00m, 15000.00m, null, "Figura Coleccionable Edición Limitada", "https://images.unsplash.com/photo-1607604276583-eef5d076aa5f?w=500", 1, 1u },
                    { 4, 3, "Indumentaria clásica", "FINALIZADA", new DateTime(2026, 10, 14, 1, 46, 39, 196, DateTimeKind.Utc).AddTicks(6433), new DateTime(2026, 10, 12, 3, 46, 39, 196, DateTimeKind.Utc).AddTicks(6431), 2, 2000.00m, 20000.00m, 25000.00m, "Campera de Cuero Vintage", "https://images.unsplash.com/photo-1551028719-00167b16eac5?w=500", 1, 2u },
                    { 5, 4, "Sin ofertas registradas", "DESIERTA", new DateTime(2026, 10, 13, 3, 46, 39, 196, DateTimeKind.Utc).AddTicks(6438), new DateTime(2026, 10, 11, 3, 46, 39, 196, DateTimeKind.Utc).AddTicks(6437), null, 10000.00m, 80000.00m, null, "Repuesto Clásico de Vehículo", "https://images.unsplash.com/photo-1486006920555-c77dce18193b?w=500", 1, 1u }
                });

            migrationBuilder.InsertData(
                table: "Pujas",
                columns: new[] { "Id", "FechaCreacion", "Monto", "SubastaId", "UsuarioId" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 10, 14, 3, 31, 39, 196, DateTimeKind.Utc).AddTicks(6500), 35000.00m, 1, 3 },
                    { 2, new DateTime(2026, 10, 14, 3, 41, 39, 196, DateTimeKind.Utc).AddTicks(6501), 45000.00m, 1, 2 },
                    { 3, new DateTime(2026, 10, 13, 3, 46, 39, 196, DateTimeKind.Utc).AddTicks(6503), 25000.00m, 4, 2 }
                });

            migrationBuilder.InsertData(
                table: "TransactionLedgers",
                columns: new[] { "Id", "BilleteraId", "Fecha", "Monto", "SubastaId", "Tipo" },
                values: new object[,]
                {
                    { 1, 2, new DateTime(2026, 10, 13, 3, 46, 39, 196, DateTimeKind.Utc).AddTicks(6524), 150000.00m, null, 0 },
                    { 2, 2, new DateTime(2026, 10, 14, 3, 41, 39, 196, DateTimeKind.Utc).AddTicks(6525), 45000.00m, 1, 1 },
                    { 3, 3, new DateTime(2026, 10, 13, 3, 46, 39, 196, DateTimeKind.Utc).AddTicks(6527), 200000.00m, null, 0 },
                    { 4, 3, new DateTime(2026, 10, 14, 3, 31, 39, 196, DateTimeKind.Utc).AddTicks(6528), 35000.00m, 1, 1 },
                    { 5, 3, new DateTime(2026, 10, 14, 3, 41, 39, 196, DateTimeKind.Utc).AddTicks(6530), 35000.00m, 1, 2 }
                });
        }
    }
}
