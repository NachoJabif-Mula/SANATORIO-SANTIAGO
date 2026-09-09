using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaresFamilia.Infrastructure.Migrations.Nube
{
    /// <inheritdoc />
    public partial class SimplifyAnulacionSchemaToComandaItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DROP TABLE IF EXISTS (en vez de DropTable): el historial de migraciones registra
            // "AddAnulacionComanda" como aplicada, pero la tabla física no llegó a existir en esta
            // base (quedó en un estado inconsistente por un fallback previo a EnsureCreated). Sin
            // este guard, esta migración fallaba con "table does not exist" y bloqueaba todo el
            // resto de la cadena de migraciones detrás de ella.
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"AnulacionesComanda\" CASCADE;");

            migrationBuilder.AddColumn<Guid>(
                name: "AnuladoPorUsuarioId",
                table: "ComandaItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAnulacion",
                table: "ComandaItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoAnulacion",
                table: "ComandaItems",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComandaItems_AnuladoPorUsuarioId",
                table: "ComandaItems",
                column: "AnuladoPorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ComandaItems_Cancelado",
                table: "ComandaItems",
                column: "Cancelado");

            migrationBuilder.AddForeignKey(
                name: "FK_ComandaItems_Usuarios_AnuladoPorUsuarioId",
                table: "ComandaItems",
                column: "AnuladoPorUsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ComandaItems_Usuarios_AnuladoPorUsuarioId",
                table: "ComandaItems");

            migrationBuilder.DropIndex(
                name: "IX_ComandaItems_AnuladoPorUsuarioId",
                table: "ComandaItems");

            migrationBuilder.DropIndex(
                name: "IX_ComandaItems_Cancelado",
                table: "ComandaItems");

            migrationBuilder.DropColumn(
                name: "AnuladoPorUsuarioId",
                table: "ComandaItems");

            migrationBuilder.DropColumn(
                name: "FechaAnulacion",
                table: "ComandaItems");

            migrationBuilder.DropColumn(
                name: "MotivoAnulacion",
                table: "ComandaItems");

            migrationBuilder.CreateTable(
                name: "AnulacionesComanda",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComandaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComandaItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cantidad = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    MontoAnulado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SyncEstado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnulacionesComanda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnulacionesComanda_ComandaItems_ComandaItemId",
                        column: x => x.ComandaItemId,
                        principalTable: "ComandaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
    }
}
