using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using MyShowtime.Client;
using MyShowtime.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure AWS Cognito authentication
builder.Services.AddOidcAuthentication(options =>
{
    // AWS Cognito configuration
    options.ProviderOptions.Authority = "https://cognito-idp.us-east-2.amazonaws.com/us-east-2_VkwlcR2m8";
    options.ProviderOptions.ClientId = "548ed6mlir2cdkq30aphbn3had";
    options.ProviderOptions.ResponseType = "code";

    // Scopes
    options.ProviderOptions.DefaultScopes.Clear();
    options.ProviderOptions.DefaultScopes.Add("openid");
    options.ProviderOptions.DefaultScopes.Add("email");
    options.ProviderOptions.DefaultScopes.Add("profile");

    // Cognito endpoints (using hosted UI domain)
    options.ProviderOptions.MetadataUrl = "https://cognito-idp.us-east-2.amazonaws.com/us-east-2_VkwlcR2m8/.well-known/openid-configuration";

    // Redirect URIs - ensure proper path construction
    var baseUri = new Uri(builder.HostEnvironment.BaseAddress);
    options.ProviderOptions.PostLogoutRedirectUri = baseUri.ToString();
    options.ProviderOptions.RedirectUri = new Uri(baseUri, "authentication/login-callback").ToString();
});

// Configure HttpClient with automatic JWT bearer token injection
builder.Services.AddHttpClient("MyShowtime.Api", client =>
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

// Register scoped HttpClient for components
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("MyShowtime.Api"));

builder.Services.AddScoped<MediaLibraryService>();

await builder.Build().RunAsync();
