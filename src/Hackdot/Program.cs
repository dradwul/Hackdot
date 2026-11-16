using Hackdot;
using Hackdot.Core;
using Hackdot.Core.Models;
using Hackdot.Core.Services;
using Hackdot.Core.Services.Terminal;
using Hackdot.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Localization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// In Blazor WebAssembly, scoped services live as long as the app (one per browser tab).
builder.Services.AddScoped<ShellService>();

// One shared file system: the terminal and (soon) the file explorer see exactly the same files.
builder.Services.AddScoped(_ => FileSystemLoader.CreateDefault());
builder.Services.AddScoped(sp => new ShellSession(sp.GetRequiredService<VirtualFileSystem>()));
builder.Services.AddScoped(sp => new CommandInterpreter(
    sp.GetRequiredService<ShellSession>(),
    sp.GetRequiredService<IStringLocalizer<TerminalResources>>()));
builder.Services.AddScoped<TerminalService>();

await builder.Build().RunAsync();
