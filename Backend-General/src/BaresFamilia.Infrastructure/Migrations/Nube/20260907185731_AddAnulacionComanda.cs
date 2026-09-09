using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaresFamilia.Infrastructure.Migrations.Nube
{
    /// <inheritdoc />
    public partial class AddAnulacionComanda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Cancelado",
                table: "ComandaItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AnulacionesComanda",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComandaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComandaItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Cantidad = table.Column<int>(type: "integer", nullable: false),
                    MontoAnulado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SyncEstado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnulacionesComanda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnulacionesComanda_ComandaItems_ComandaItemId",
                        column: x => x.ComandaItemId,
                        principalTable: "ComandaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnulacionesComanda_Comandas_ComandaId",
                        column: x => x.ComandaId,
                        principalTable: "Comandas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnulacionesComanda_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnulacionesComanda_ComandaId",
                table: "AnulacionesComanda",
                column: "ComandaId");

            migrationBuilder.CreateIndex(
                name: "IX_AnulacionesComanda_ComandaItemId",
                table: "AnulacionesComanda",
                column: "ComandaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AnulacionesComanda_CreatedAt",
                table: "AnulacionesComanda",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AnulacionesComanda_IsActive",
                table: "AnulacionesComanda",
                column: "IsActive",
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_AnulacionesComanda_SyncEstado",
                table: "AnulacionesComanda",
                column: "SyncEstado");

            migrationBuilder.CreateIndex(
                name: "IX_AnulacionesComanda_UpdatedAt",
                table: "AnulacionesComanda",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AnulacionesComanda_UsuarioId",
                table: "AnulacionesComanda",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnulacionesComanda");

            migrationBuilder.DropColumn(
                name: "Cancelado",
                table: "ComandaItems");
        }
    }
}
