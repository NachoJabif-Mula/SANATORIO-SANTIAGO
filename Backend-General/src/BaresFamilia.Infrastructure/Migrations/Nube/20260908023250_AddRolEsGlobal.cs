using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaresFamilia.Infrastructure.Migrations.Nube
{
    /// <inheritdoc />
    public partial class AddRolEsGlobal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsGlobal",
                table: "Roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsGlobal",
                table: "Roles");
        }
    }
}
