using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaresFamilia.Infrastructure.Migrations.Local
{
    /// <inheritdoc />
    public partial class AddCuentaCorrienteExtendida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsCuentaCorriente",
                table: "MetodosPago",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Apellido",
                table: "Clientes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Clientes",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SyncEstado",
                table: "Clientes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "MovimientosCuentaCorriente",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaCorrienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComandaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Detalle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SyncEstado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosCuentaCorriente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosCuentaCorriente_Comandas_ComandaId",
                        column: x => x.ComandaId,
                        principalTable: "Comandas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MovimientosCuentaCorriente_CuentasCorrientes_CuentaCorrient~",
                        column: x => x.CuentaCorrienteId,
                        principalTable: "CuentasCorrientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrintJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImpresoraId = table.Column<Guid>(type: "uuid", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    TipoDocumento = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    ResultadoJson = table.Column<string>(type: "text", nullable: true),
                    Intentos = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintJobs_Impresoras_ImpresoraId",
                        column: x => x.ImpresoraId,
                        principalTable: "Impresoras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PrintJobs_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Nombre_Apellido",
                table: "Clientes",
                columns: new[] { "Nombre", "Apellido" });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_SyncEstado",
                table: "Clientes",
                column: "SyncEstado");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCuentaCorriente_ComandaId",
                table: "MovimientosCuentaCorriente",
                column: "ComandaId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCuentaCorriente_CreatedAt",
                table: "MovimientosCuentaCorriente",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCuentaCorriente_CuentaCorrienteId",
                table: "MovimientosCuentaCorriente",
                column: "CuentaCorrienteId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCuentaCorriente_IsActive",
                table: "MovimientosCuentaCorriente",
                column: "IsActive",
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCuentaCorriente_SyncEstado",
                table: "MovimientosCuentaCorriente",
                column: "SyncEstado");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCuentaCorriente_UpdatedAt",
                table: "MovimientosCuentaCorriente",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_ImpresoraId",
                table: "PrintJobs",
                column: "ImpresoraId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_SucursalId",
                table: "PrintJobs",
                column: "SucursalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovimientosCuentaCorriente");

            migrationBuilder.DropTable(
                name: "PrintJobs");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_Nombre_Apellido",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_SyncEstado",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "EsCuentaCorriente",
                table: "MetodosPago");

            migrationBuilder.DropColumn(
                name: "Apellido",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "SyncEstado",
                table: "Clientes");
        }
    }
}
