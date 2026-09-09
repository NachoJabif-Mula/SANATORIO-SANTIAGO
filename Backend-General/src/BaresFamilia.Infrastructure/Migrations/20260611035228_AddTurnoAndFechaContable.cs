using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaresFamilia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTurnoAndFechaContable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaContable",
                table: "TurnosCaja",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Turno",
                table: "TurnosCaja",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaContable",
                table: "Comandas",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Turno",
                table: "Comandas",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_TurnosCaja_FechaContable_Turno",
                table: "TurnosCaja",
                columns: new[] { "FechaContable", "Turno" });

            migrationBuilder.CreateIndex(
                name: "IX_Comandas_FechaContable_Turno",
                table: "Comandas",
                columns: new[] { "FechaContable", "Turno" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TurnosCaja_FechaContable_Turno",
                table: "TurnosCaja");

            migrationBuilder.DropIndex(
                name: "IX_Comandas_FechaContable_Turno",
                table: "Comandas");

            migrationBuilder.DropColumn(
                name: "FechaContable",
                table: "TurnosCaja");

            migrationBuilder.DropColumn(
                name: "Turno",
                table: "TurnosCaja");

            migrationBuilder.DropColumn(
                name: "FechaContable",
                table: "Comandas");

            migrationBuilder.DropColumn(
                name: "Turno",
                table: "Comandas");
        }
    }
}
