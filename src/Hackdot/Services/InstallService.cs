using Microsoft.JSInterop;

namespace Hackdot.Services;

/// <summary>Whether Hackdot can be installed as an app in this browser.</summary>
public enum InstallState
{
    /// <summary>The browser can't install web apps (or hasn't offered to yet).</summary>
    None,

    /// <summary>The browser's own install dialog can be shown (Chrome, Edge, Samsung Internet, …).</summary>
    Prompt,

    /// <summary>iOS: the player adds Hackdot from the share menu, so we show how.</summary>
    Manual,

    /// <summary>Hackdot is already running as an installed app.</summary>
    Installed,
}

/// <summary>Offers an "Install" shortcut instead of the browser's menu (see wwwroot/js/install.js).</summary>
public sealed class InstallService(IJSRuntime js)
{
    private DotNetObjectReference<InstallService>? _self;

    public InstallState State { get; private set; } = InstallState.None;

    public bool CanInstall => State is InstallState.Prompt or InstallState.Manual;

    /// <summary>The "Add to Home Screen" instructions are showing (iOS).</summary>
    public bool IsHelpOpen { get; private set; }

    public event Action? Changed;

    public async Task InitializeAsync()
    {
        if (_self is not null)
        {
            return;
        }

        _self = DotNetObjectReference.Create(this);
        try
        {
            SetState(await js.InvokeAsync<string>("hackdotInstall.subscribe", _self));
        }
        catch (JSException)
        {
            // install.js isn't loaded – there's simply no install shortcut.
        }
    }

    [JSInvokable]
    public void OnInstallStateChanged(string state) => SetState(state);

    public async Task InstallAsync()
    {
        switch (State)
        {
            case InstallState.Prompt:
                await js.InvokeAsync<string>("hackdotInstall.prompt");
                break;
            case InstallState.Manual:
                IsHelpOpen = true;
                Changed?.Invoke();
                break;
        }
    }

    public void CloseHelp()
    {
        IsHelpOpen = false;
        Changed?.Invoke();
    }

    private void SetState(string value)
    {
        var state = value switch
        {
            "prompt" => InstallState.Prompt,
            "manual" => InstallState.Manual,
            "installed" => InstallState.Installed,
            _ => InstallState.None,
        };

        if (state != State)
        {
            State = state;
            Changed?.Invoke();
        }
    }
}
