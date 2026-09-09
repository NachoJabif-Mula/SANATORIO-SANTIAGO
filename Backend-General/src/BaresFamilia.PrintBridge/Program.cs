using BaresFamilia.PrintBridge;

var builder = Host.CreateApplicationBuilder(args);

// Configurar como servicio de Windows si corre bajo el SCM de Windows
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "BaresFamilia PrintBridge Service";
});

builder.Services.AddHostedService<PrintBridgeWorker>();

var host = builder.Build();
host.Run();
