using System.Diagnostics;
using System.Text.Json;
using BaresFamilia.Fiscal.Cli;
using BaresFamilia.Fiscal.Engine;

var sw = Stopwatch.StartNew();

try
{
    string inputJson = string.Empty;

    // 1. Leer entrada JSON de archivo o stdin
    if (args.Length > 0 && File.Exists(args[0]))
    {
        inputJson = await File.ReadAllTextAsync(args[0]);
    }
    else
    {
        using var reader = new StreamReader(Console.OpenStandardInput());
        inputJson = await reader.ReadToEndAsync();
    }

    if (string.IsNullOrWhiteSpace(inputJson))
    {
        Console.WriteLine(JsonSerializer.Serialize(FiscalJobOutput.FromResult(FiscalResult.Error("No se recibieron datos JSON."), 0)));
        return 1;
    }

    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var input = JsonSerializer.Deserialize<FiscalJobInput>(inputJson, options);

    if (input == null || input.Config == null)
    {
        Console.WriteLine(JsonSerializer.Serialize(FiscalJobOutput.FromResult(FiscalResult.Error("Formato JSON o Configuración no válidos."), 0)));
        return 1;
    }

    // 2. Obtener Driver Fiscal
    IFiscalDriver driver = FiscalDriverFactory.GetDriver(input.Config.Driver);

    // 3. Ejecutar Operación Solicitada
    FiscalResult result = input.Operacion.ToLowerInvariant().Trim() switch
    {
        "factura" => await driver.ImprimirFacturaAsync(input.Config, input.Request),
        "notacredito" or "nc" => await driver.ImprimirNotaCreditoAsync(input.Config, input.Request),
        "cierrez" => await driver.EjecutarCierreZAsync(input.Config),
        "cierrex" => await driver.EjecutarCierreXAsync(input.Config),
        "cancelar" => await driver.CancelarComprobanteAsync(input.Config),
        "consultarestado" or "estado" => await driver.ConsultarEstadoAsync(input.Config),
        _ => FiscalResult.Error($"Operación fiscal '{input.Operacion}' no soportada.")
    };

    sw.Stop();

    // 4. Escribir resultado JSON en stdout
    var output = FiscalJobOutput.FromResult(result, sw.ElapsedMilliseconds);
    Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = false }));

    return result.Success ? 0 : 1;
}
catch (Exception ex)
{
    sw.Stop();
    var output = FiscalJobOutput.FromResult(FiscalResult.Error($"Excepción no controlada en Fiscal.Cli: {ex.Message}"), sw.ElapsedMilliseconds);
    Console.WriteLine(JsonSerializer.Serialize(output));
    return 1;
}
