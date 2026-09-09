using System.Text.Json;
using BaresFamilia.Printing.Cli;
using BaresFamilia.Printing.Cli.Drivers;

try
{
    string inputJson = string.Empty;

    // 1. Leer de archivo o stdin
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
        Console.WriteLine(JsonSerializer.Serialize(PrintResult.Error("No se recibieron datos JSON por stdin ni argumento de archivo.")));
        return 1;
    }

    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var input = JsonSerializer.Deserialize<PrintJobInput>(inputJson, options);

    if (input == null)
    {
        Console.WriteLine(JsonSerializer.Serialize(PrintResult.Error("Formato JSON de entrada no válido.")));
        return 1;
    }

    // 2. Seleccionar Driver
    IPrinterDriver driver = input.ConnectionType.ToLowerInvariant() switch
    {
        "usb" => new WindowsPrinterDriver(),
        "emulator" or "emulador" => new EmulatorPrinterDriver(),
        _ => new TcpPrinterDriver()
    };

    // 3. Ejecutar Impresión
    var result = await driver.PrintAsync(input);

    // 4. Salida JSON por stdout
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false }));
    return result.Success ? 0 : 1;
}
catch (Exception ex)
{
    Console.WriteLine(JsonSerializer.Serialize(PrintResult.Error($"Excepción no controlada en Printing.Cli: {ex.Message}")));
    return 1;
}
