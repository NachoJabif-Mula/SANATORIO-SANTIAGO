using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaresFamilia.Infrastructure.Migrations.Nube
{
    /// <inheritdoc />
    public partial class AddSucursalIdToCategoriaProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SucursalId",
                table: "Productos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SucursalId",
                table: "Categorias",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Backfill: el catálogo (categorías y productos) pasa a ser propio de cada
            // sucursal. Las filas que ya existan (ambiente de desarrollo) se asignan a
            // la primera sucursal registrada, para no dejar el FK apuntando a nada y
            // que se puedan reasignar a mano desde el Backoffice si hace falta.
            migrationBuilder.Sql(
                """
                UPDATE "Categorias" SET "SucursalId" = (SELECT "Id" FROM "Sucursales" ORDER BY "CreatedAt" LIMIT 1)
                WHERE "SucursalId" = '00000000-0000-0000-0000-000000000000' AND EXISTS (SELECT 1 FROM "Sucursales");
                UPDATE "Productos" SET "SucursalId" = (SELECT "Id" FROM "Sucursales" ORDER BY "CreatedAt" LIMIT 1)
                WHERE "SucursalId" = '00000000-0000-0000-0000-000000000000' AND EXISTS (SELECT 1 FROM "Sucursales");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_SucursalId",
                table: "Productos",
                column: "SucursalId");

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_SucursalId",
                table: "Categorias",
                column: "SucursalId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categorias_Sucursales_SucursalId",
                table: "Categorias",
                column: "SucursalId",
                principalTable: "Sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Sucursales_SucursalId",
                table: "Productos",
                column: "SucursalId",
                principalTable: "Sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categorias_Sucursales_SucursalId",
                table: "Categorias");

            migrationBuilder.DropForeignKey(
                name: "FK_Productos_Sucursales_SucursalId",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Productos_SucursalId",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Categorias_SucursalId",
                table: "Categorias");

            migrationBuilder.DropColumn(
                name: "SucursalId",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "SucursalId",
                table: "Categorias");
        }
    }
}
