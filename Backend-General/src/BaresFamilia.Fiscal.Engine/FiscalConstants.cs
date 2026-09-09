namespace BaresFamilia.Fiscal.Engine;

/// <summary>
/// Constantes y códigos oficiales de AFIP / ARCA para operaciones fiscales.
/// </summary>
public static class FiscalConstants
{
    // Tipos de comprobante AFIP
    public const int COMP_FACTURA_A = 1;
    public const int COMP_NOTA_DEBITO_A = 2;
    public const int COMP_NOTA_CREDITO_A = 3;
    public const int COMP_FACTURA_B = 6;
    public const int COMP_NOTA_DEBITO_B = 7;
    public const int COMP_NOTA_CREDITO_B = 8;
    public const int COMP_FACTURA_C = 11;
    public const int COMP_NOTA_DEBITO_C = 12;
    public const int COMP_NOTA_CREDITO_C = 13;

    // Alícuotas de IVA (códigos AFIP eIvaType)
    public const int IVA_NO_GRAVADO = 0;
    public const int IVA_EXENTO = 1;
    public const int IVA_10_5 = 4;
    public const int IVA_21 = 5;
    public const int IVA_27 = 6;

    // Formas de pago AFIP
    public const int PAGO_EFECTIVO = 1;
    public const int PAGO_TARJETA_DEBITO = 2;
    public const int PAGO_TARJETA_CREDITO = 3;
    public const int PAGO_CUENTA_CORRIENTE = 4;
    public const int PAGO_CHEQUE = 5;
    public const int PAGO_OTROS = 99;

    // Condición IVA del cliente
    public const int COND_RESPONSABLE_INSCRIPTO = 1;
    public const int COND_EXENTO = 4;
    public const int COND_CONSUMIDOR_FINAL = 5;
    public const int COND_MONOTRIBUTO = 6;
}
