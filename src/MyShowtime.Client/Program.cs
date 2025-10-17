using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MyShowtime.Client;
using MyShowtime.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<MediaLibraryService>();
builder.Services.AddSingleton<UserStateService>();

var host = builder.Build();

// Initialize user state from localStorage
var userStateService = host.Services.GetRequiredService<UserStateService>();
await userStateService.InitializeAsync();

await host.RunAsync();
