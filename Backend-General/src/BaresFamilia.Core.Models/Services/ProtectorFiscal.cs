using BaresFamilia.Core.Models.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace BaresFamilia.Core.Models.Services;

/// <summary>
/// Implementación de <see cref="IProtectorFiscal"/> sobre Data Protection.
///
/// Las claves se persisten por instalación (ver DataProtection:KeyRingPath): lo cifrado por
/// la Nube solo lo descifra la Nube, y lo cifrado por una sucursal solo esa sucursal. El
/// traspaso del certificado de la Nube a la sucursal se hace descifrando en origen y
/// volviendo a cifrar en destino, nunca copiando la base cifrada entre instalaciones.
/// </summary>
public class ProtectorFiscal : IProtectorFiscal
{
    private const string PropositoProtector = "BaresFamilia.Fiscal.Certificados.v1";

    private readonly IDataProtector _protector;

    public ProtectorFiscal(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(PropositoProtector);
    }

    public byte[] Proteger(byte[] datos) => _protector.Protect(datos);

    public byte[] Desproteger(byte[] datosProtegidos) => _protector.Unprotect(datosProtegidos);

    public string ProtegerTexto(string texto) => _protector.Protect(texto);

    public string DesprotegerTexto(string textoProtegido) => _protector.Unprotect(textoProtegido);
}
