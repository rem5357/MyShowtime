using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MyShowtime.Client;
using MyShowtime.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Register UserStateService as singleton first (needs to be available for the handler)
builder.Services.AddSingleton<UserStateService>();

// Register the authenticated HTTP message handler
builder.Services.AddScoped<AuthenticatedHttpMessageHandler>();

// Configure HttpClient with the authenticated handler
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthenticatedHttpMessageHandler>();
    handler.InnerHandler = new HttpClientHandler();

    var httpClient = new HttpClient(handler)
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
    };

    return httpClient;
});

builder.Services.AddScoped<MediaLibraryService>();

var host = builder.Build();

// Initialize user state from localStorage
var userStateService = host.Services.GetRequiredService<UserStateService>();
await userStateService.InitializeAsync();

await host.RunAsync();
