namespace BaresFamilia.Core.Models.Enums;

/// <summary>
/// Estado de sincronización para entidades transaccionales en el flujo offline/online.
/// </summary>
public enum SyncEstado
{
    Pendiente = 0,
    Sincronizado = 1,
    Conflicto = 2
}
