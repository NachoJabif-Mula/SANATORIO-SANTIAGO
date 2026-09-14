using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaresFamilia.Infrastructure.Migrations.Nube
{
    /// <inheritdoc />
    public partial class AgregarClavePrivadaCsr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ClavePrivadaCifrada",
                table: "ConfiguracionesFiscales",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CsrGeneradoEn",
                table: "ConfiguracionesFiscales",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CsrSubject",
                table: "ConfiguracionesFiscales",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClavePrivadaCifrada",
                table: "ConfiguracionesFiscales");

            migrationBuilder.DropColumn(
                name: "CsrGeneradoEn",
                table: "ConfiguracionesFiscales");

            migrationBuilder.DropColumn(
                name: "CsrSubject",
                table: "ConfiguracionesFiscales");
        }
    }
}
