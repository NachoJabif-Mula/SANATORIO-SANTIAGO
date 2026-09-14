using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaresFamilia.Infrastructure.Migrations.Nube
{
    /// <inheritdoc />
    public partial class RenombrarAuditoriaComprobanteAXml : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ResponseJson",
                table: "Comprobantes",
                newName: "ResponseXml");

            migrationBuilder.RenameColumn(
                name: "RequestJson",
                table: "Comprobantes",
                newName: "RequestXml");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ResponseXml",
                table: "Comprobantes",
                newName: "ResponseJson");

            migrationBuilder.RenameColumn(
                name: "RequestXml",
                table: "Comprobantes",
                newName: "RequestJson");
        }
    }
}
