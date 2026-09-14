namespace BaresFamilia.Core.Models.Dtos.Impresion;

/// <summary>
/// Trabajo de la cola de impresión tal como lo monitorea el Backoffice.
/// </summary>
public record PrintJobDto(
    Guid Id,
    Guid SucursalId,
    Guid ImpresoraId,
    string ImpresoraNombre,
    string Estado,
    string TipoDocumento,
    string PayloadJson,
    string? ResultadoJson,
    int Intentos,
    DateTime CreatedAt
);
