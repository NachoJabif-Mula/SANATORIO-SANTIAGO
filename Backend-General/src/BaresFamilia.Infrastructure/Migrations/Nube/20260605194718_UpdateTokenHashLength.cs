using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaresFamilia.Infrastructure.Migrations.Nube
{
    /// <inheritdoc />
    public partial class UpdateTokenHashLength : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "DispositivosActivacion",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Impresoras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TipoConexion = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Direccion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Puerto = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Impresoras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Impresoras_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TiposTicket",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TemplateContenido = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposTicket", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImpresoraTicketTipos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImpresoraId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoTicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpresoraTicketTipos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImpresoraTicketTipos_Impresoras_ImpresoraId",
                        column: x => x.ImpresoraId,
                        principalTable: "Impresoras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImpresoraTicketTipos_TiposTicket_TipoTicketId",
                        column: x => x.TipoTicketId,
                        principalTable: "TiposTicket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Impresoras_IsActive",
                table: "Impresoras",
                column: "IsActive",
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Impresoras_SucursalId_Nombre",
                table: "Impresoras",
                columns: new[] { "SucursalId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Impresoras_UpdatedAt",
                table: "Impresoras",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ImpresoraTicketTipos_ImpresoraId_TipoTicketId",
                table: "ImpresoraTicketTipos",
                columns: new[] { "ImpresoraId", "TipoTicketId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImpresoraTicketTipos_IsActive",
                table: "ImpresoraTicketTipos",
                column: "IsActive",
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ImpresoraTicketTipos_TipoTicketId",
                table: "ImpresoraTicketTipos",
                column: "TipoTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_ImpresoraTicketTipos_UpdatedAt",
                table: "ImpresoraTicketTipos",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TiposTicket_Codigo",
                table: "TiposTicket",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TiposTicket_IsActive",
                table: "TiposTicket",
                column: "IsActive",
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_TiposTicket_UpdatedAt",
                table: "TiposTicket",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImpresoraTicketTipos");

            migrationBuilder.DropTable(
                name: "Impresoras");

            migrationBuilder.DropTable(
                name: "TiposTicket");

            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "DispositivosActivacion",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
