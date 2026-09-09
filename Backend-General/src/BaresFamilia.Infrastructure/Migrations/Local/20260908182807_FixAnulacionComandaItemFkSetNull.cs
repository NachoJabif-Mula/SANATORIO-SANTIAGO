using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaresFamilia.Infrastructure.Migrations.Local
{
    /// <inheritdoc />
    public partial class FixAnulacionComandaItemFkSetNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnulacionesComanda_ComandaItems_ComandaItemId",
                table: "AnulacionesComanda");

            migrationBuilder.AddForeignKey(
                name: "FK_AnulacionesComanda_ComandaItems_ComandaItemId",
                table: "AnulacionesComanda",
                column: "ComandaItemId",
                principalTable: "ComandaItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnulacionesComanda_ComandaItems_ComandaItemId",
                table: "AnulacionesComanda");

            migrationBuilder.AddForeignKey(
                name: "FK_AnulacionesComanda_ComandaItems_ComandaItemId",
                table: "AnulacionesComanda",
                column: "ComandaItemId",
                principalTable: "ComandaItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
