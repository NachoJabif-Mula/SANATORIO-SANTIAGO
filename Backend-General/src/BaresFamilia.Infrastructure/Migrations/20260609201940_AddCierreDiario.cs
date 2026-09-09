using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaresFamilia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCierreDiario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CierresDiarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CajaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsuarioCierreId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalVentas = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalEgresos = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalNeto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ResumenJson = table.Column<string>(type: "text", nullable: true),
                    Observaciones = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SyncEstado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CierresDiarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CierresDiarios_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CierresDiarios_Usuarios_UsuarioCierreId",
                        column: x => x.UsuarioCierreId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CierresDiarios_CajaId_Fecha",
                table: "CierresDiarios",
                columns: new[] { "CajaId", "Fecha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CierresDiarios_Fecha",
                table: "CierresDiarios",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_CierresDiarios_IsActive",
                table: "CierresDiarios",
                column: "IsActive",
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_CierresDiarios_SyncEstado",
                table: "CierresDiarios",
                column: "SyncEstado");

            migrationBuilder.CreateIndex(
                name: "IX_CierresDiarios_UpdatedAt",
                table: "CierresDiarios",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CierresDiarios_UsuarioCierreId",
                table: "CierresDiarios",
                column: "UsuarioCierreId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CierresDiarios");
        }
    }
}
