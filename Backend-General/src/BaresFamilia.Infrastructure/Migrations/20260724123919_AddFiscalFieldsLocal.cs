using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaresFamilia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalFieldsLocal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CondicionIva",
                table: "Sucursales",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cuit",
                table: "Sucursales",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DomicilioFiscal",
                table: "Sucursales",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaInicioActividades",
                table: "Sucursales",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroIIBB",
                table: "Sucursales",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PuntoDeVenta",
                table: "Sucursales",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RazonSocial",
                table: "Sucursales",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AlicuotaIva",
                table: "Productos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TipoDispositivo",
                table: "Impresoras",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Velocidad",
                table: "Impresoras",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CondicionIva",
                table: "Sucursales");

            migrationBuilder.DropColumn(
                name: "Cuit",
                table: "Sucursales");

            migrationBuilder.DropColumn(
                name: "DomicilioFiscal",
                table: "Sucursales");

            migrationBuilder.DropColumn(
                name: "FechaInicioActividades",
                table: "Sucursales");

            migrationBuilder.DropColumn(
                name: "NumeroIIBB",
                table: "Sucursales");

            migrationBuilder.DropColumn(
                name: "PuntoDeVenta",
                table: "Sucursales");

            migrationBuilder.DropColumn(
                name: "RazonSocial",
                table: "Sucursales");

            migrationBuilder.DropColumn(
                name: "AlicuotaIva",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "TipoDispositivo",
                table: "Impresoras");

            migrationBuilder.DropColumn(
                name: "Velocidad",
                table: "Impresoras");
        }
    }
}
