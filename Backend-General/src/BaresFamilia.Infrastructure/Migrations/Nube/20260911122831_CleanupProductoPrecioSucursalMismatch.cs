using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaresFamilia.Infrastructure.Migrations.Nube
{
    /// <inheritdoc />
    public partial class CleanupProductoPrecioSucursalMismatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Un producto vive en una sola sucursal, y su precio también: cualquier
            // ProductoPrecio que haya quedado apuntando a una sucursal distinta de la
            // de su producto (del viejo modelo, donde el catálogo era compartido) ya
            // no tiene sentido — esa sucursal ni siquiera tiene el producto en su
            // catálogo. Se borran en vez de desactivar porque son basura, no historial.
            migrationBuilder.Sql(
                """
                DELETE FROM "ProductoPrecios" pp
                USING "Productos" p
                WHERE pp."ProductoId" = p."Id" AND pp."SucursalId" <> p."SucursalId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
