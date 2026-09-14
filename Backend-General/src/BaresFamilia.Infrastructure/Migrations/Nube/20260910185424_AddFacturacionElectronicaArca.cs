using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaresFamilia.Infrastructure.Migrations.Nube
{
    /// <inheritdoc />
    public partial class AddFacturacionElectronicaArca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PrintJobs_Impresoras_ImpresoraId",
                table: "PrintJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_PrintJobs_Sucursales_SucursalId",
                table: "PrintJobs");

            migrationBuilder.DropIndex(
                name: "IX_PrintJobs_SucursalId",
                table: "PrintJobs");

            migrationBuilder.AlterColumn<string>(
                name: "TipoDocumento",
                table: "PrintJobs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "PrintJobs",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "Estado",
                table: "PrintJobs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<Guid>(
                name: "ComprobanteId",
                table: "PrintJobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaProcesado",
                table: "PrintJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SyncEstado",
                table: "PrintJobs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pendiente");

            migrationBuilder.AddColumn<string>(
                name: "UltimoError",
                table: "PrintJobs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Comprobantes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComandaId = table.Column<Guid>(type: "uuid", nullable: true),
                    TipoComprobante = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Ambiente = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PuntoVenta = table.Column<int>(type: "integer", nullable: false),
                    NumeroComprobante = table.Column<long>(type: "bigint", nullable: false),
                    FechaEmision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CuitEmisor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RazonSocialEmisor = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    CondicionIvaEmisor = table.Column<int>(type: "integer", nullable: false),
                    TipoDocumentoReceptor = table.Column<int>(type: "integer", nullable: false),
                    NumeroDocumentoReceptor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RazonSocialReceptor = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    CondicionIvaReceptor = table.Column<int>(type: "integer", nullable: false),
                    ImporteNeto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ImporteIva = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ImporteExento = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ImporteNoGravado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ImporteTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Cae = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CaeVencimiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    QrPayload = table.Column<string>(type: "text", nullable: true),
                    ObservacionesArca = table.Column<string>(type: "text", nullable: true),
                    IntentosEmision = table.Column<int>(type: "integer", nullable: false),
                    FechaUltimoIntento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UltimoError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RequestJson = table.Column<string>(type: "text", nullable: true),
                    ResponseJson = table.Column<string>(type: "text", nullable: true),
                    SyncEstado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comprobantes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comprobantes_Comandas_ComandaId",
                        column: x => x.ComandaId,
                        principalTable: "Comandas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Comprobantes_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionesFiscales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ambiente = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CertificadoCifrado = table.Column<byte[]>(type: "bytea", nullable: true),
                    CertificadoPasswordCifrada = table.Column<string>(type: "text", nullable: true),
                    CertificadoNombreArchivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CertificadoSubject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CertificadoThumbprint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CertificadoVence = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CertificadoCargadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UltimaValidacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UltimaValidacionOk = table.Column<bool>(type: "boolean", nullable: false),
                    UltimaValidacionMensaje = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesFiscales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfiguracionesFiscales_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketsAccesoWsaa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ambiente = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Servicio = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    Sign = table.Column<string>(type: "text", nullable: false),
                    GeneradoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiraEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketsAccesoWsaa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketsAccesoWsaa_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComprobanteAlicuotas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComprobanteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Alicuota = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BaseImponible = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Importe = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComprobanteAlicuotas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComprobanteAlicuotas_Comprobantes_ComprobanteId",
                        column: x => x.ComprobanteId,
                        principalTable: "Comprobantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_ComprobanteId",
                table: "PrintJobs",
                column: "ComprobanteId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_Estado",
                table: "PrintJobs",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_IsActive",
                table: "PrintJobs",
                column: "IsActive",
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_SucursalId_CreatedAt",
                table: "PrintJobs",
                columns: new[] { "SucursalId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_SyncEstado",
                table: "PrintJobs",
                column: "SyncEstado");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_UpdatedAt",
                table: "PrintJobs",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ComprobanteAlicuotas_ComprobanteId_Alicuota",
                table: "ComprobanteAlicuotas",
                columns: new[] { "ComprobanteId", "Alicuota" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComprobanteAlicuotas_IsActive",
                table: "ComprobanteAlicuotas",
                column: "IsActive",
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ComprobanteAlicuotas_UpdatedAt",
                table: "ComprobanteAlicuotas",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Comprobantes_ComandaId",
                table: "Comprobantes",
                column: "ComandaId");

            migrationBuilder.CreateIndex(
                name: "IX_Comprobantes_Estado",
                table: "Comprobantes",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Comprobantes_IsActive",
                table: "Comprobantes",
                column: "IsActive",
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Comprobantes_SucursalId_FechaEmision",
                table: "Comprobantes",
                columns: new[] { "SucursalId", "FechaEmision" });

            migrationBuilder.CreateIndex(
                name: "IX_Comprobantes_SucursalId_PuntoVenta_TipoComprobante_NumeroCo~",
                table: "Comprobantes",
                columns: new[] { "SucursalId", "PuntoVenta", "TipoComprobante", "NumeroComprobante" },
                unique: true,
                filter: "\"NumeroComprobante\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_Comprobantes_SyncEstado",
                table: "Comprobantes",
                column: "SyncEstado");

            migrationBuilder.CreateIndex(
                name: "IX_Comprobantes_UpdatedAt",
                table: "Comprobantes",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesFiscales_IsActive",
                table: "ConfiguracionesFiscales",
                column: "IsActive",
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesFiscales_SucursalId",
                table: "ConfiguracionesFiscales",
                column: "SucursalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesFiscales_UpdatedAt",
                table: "ConfiguracionesFiscales",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TicketsAccesoWsaa_ExpiraEn",
                table: "TicketsAccesoWsaa",
                column: "ExpiraEn");

            migrationBuilder.CreateIndex(
                name: "IX_TicketsAccesoWsaa_IsActive",
                table: "TicketsAccesoWsaa",
                column: "IsActive",
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_TicketsAccesoWsaa_SucursalId_Ambiente_Servicio",
                table: "TicketsAccesoWsaa",
                columns: new[] { "SucursalId", "Ambiente", "Servicio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketsAccesoWsaa_UpdatedAt",
                table: "TicketsAccesoWsaa",
                column: "UpdatedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_PrintJobs_Comprobantes_ComprobanteId",
                table: "PrintJobs",
                column: "ComprobanteId",
                principalTable: "Comprobantes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PrintJobs_Impresoras_ImpresoraId",
                table: "PrintJobs",
                column: "ImpresoraId",
                principalTable: "Impresoras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PrintJobs_Sucursales_SucursalId",
                table: "PrintJobs",
                column: "SucursalId",
                principalTable: "Sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PrintJobs_Comprobantes_ComprobanteId",
                table: "PrintJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_PrintJobs_Impresoras_ImpresoraId",
                table: "PrintJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_PrintJobs_Sucursales_SucursalId",
                table: "PrintJobs");

            migrationBuilder.DropTable(
                name: "ComprobanteAlicuotas");

            migrationBuilder.DropTable(
                name: "ConfiguracionesFiscales");

            migrationBuilder.DropTable(
                name: "TicketsAccesoWsaa");

            migrationBuilder.DropTable(
                name: "Comprobantes");

            migrationBuilder.DropIndex(
                name: "IX_PrintJobs_ComprobanteId",
                table: "PrintJobs");

            migrationBuilder.DropIndex(
                name: "IX_PrintJobs_Estado",
                table: "PrintJobs");

            migrationBuilder.DropIndex(
                name: "IX_PrintJobs_IsActive",
                table: "PrintJobs");

            migrationBuilder.DropIndex(
                name: "IX_PrintJobs_SucursalId_CreatedAt",
                table: "PrintJobs");

            migrationBuilder.DropIndex(
                name: "IX_PrintJobs_SyncEstado",
                table: "PrintJobs");

            migrationBuilder.DropIndex(
                name: "IX_PrintJobs_UpdatedAt",
                table: "PrintJobs");

            migrationBuilder.DropColumn(
                name: "ComprobanteId",
                table: "PrintJobs");

            migrationBuilder.DropColumn(
                name: "FechaProcesado",
                table: "PrintJobs");

            migrationBuilder.DropColumn(
                name: "SyncEstado",
                table: "PrintJobs");

            migrationBuilder.DropColumn(
                name: "UltimoError",
                table: "PrintJobs");

            migrationBuilder.AlterColumn<string>(
                name: "TipoDocumento",
                table: "PrintJobs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "PrintJobs",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "Estado",
                table: "PrintJobs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_SucursalId",
                table: "PrintJobs",
                column: "SucursalId");

            migrationBuilder.AddForeignKey(
                name: "FK_PrintJobs_Impresoras_ImpresoraId",
                table: "PrintJobs",
                column: "ImpresoraId",
                principalTable: "Impresoras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PrintJobs_Sucursales_SucursalId",
                table: "PrintJobs",
                column: "SucursalId",
                principalTable: "Sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
