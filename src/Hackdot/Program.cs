using Hackdot;
using Hackdot.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Blazor WebAssembly runs one app per browser tab, so singletons are per player.
builder.Services.AddSingleton<ShellService>();

// The game owns the active mission's file system; the terminal and the file explorer both use it.
builder.Services.AddSingleton<GameService>();
builder.Services.AddSingleton<TerminalService>();
builder.Services.AddSingleton<FilesService>();
builder.Services.AddSingleton<GuideService>();

await builder.Build().RunAsync();
