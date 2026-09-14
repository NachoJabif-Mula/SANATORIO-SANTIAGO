namespace BaresFamilia.Core.Models.Interfaces;

/// <summary>
/// Cifra y descifra el material sensible de la configuración fiscal (certificado .pfx y su
/// contraseña) antes de persistirlo. El certificado habilita a emitir comprobantes con el
/// CUIT de la sucursal, por lo que nunca debe quedar en claro en la base de datos.
/// </summary>
public interface IProtectorFiscal
{
    byte[] Proteger(byte[] datos);

    byte[] Desproteger(byte[] datosProtegidos);

    string ProtegerTexto(string texto);

    string DesprotegerTexto(string textoProtegido);
}
