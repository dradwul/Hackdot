using Hackdot;
using Hackdot.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// I Blazor WebAssembly lever Scoped-tjänster lika länge som appen (en per flik).
builder.Services.AddScoped<ShellService>();

await builder.Build().RunAsync();
