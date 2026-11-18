// Lets the app offer its own "Install" button. Chromium browsers fire 'beforeinstallprompt' (often
// before Blazor has started, so the event is kept until the app asks for it). iOS has no install API,
// so the app shows instructions for "Add to Home Screen" there instead.
(() => {
    let deferredPrompt = null;

    const isStandalone = () =>
        matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;

    // iPadOS reports itself as a Mac, but Macs have no touch screen.
    const isIos = /iphone|ipad|ipod/i.test(navigator.userAgent)
        || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);

    // 'installed' (running as an app), 'prompt' (we can show the browser's install dialog),
    // 'manual' (iOS: the player adds it from the share menu) or 'none' (not installable here).
    function state() {
        if (isStandalone()) return 'installed';
        if (deferredPrompt) return 'prompt';
        if (isIos) return 'manual';
        return 'none';
    }

    const notify = () => window.hackdotInstall.listener?.invokeMethodAsync('OnInstallStateChanged', state());

    window.addEventListener('beforeinstallprompt', event => {
        // Keep the browser's own mini banner away; the app shows its own button instead.
        event.preventDefault();
        deferredPrompt = event;
        notify();
    });

    window.addEventListener('appinstalled', () => {
        deferredPrompt = null;
        notify();
    });

    window.hackdotInstall = {
        listener: null,

        // Called by InstallService with a DotNetObjectReference; returns the current state.
        subscribe(dotNetRef) {
            this.listener = dotNetRef;
            return state();
        },

        unsubscribe() {
            this.listener = null;
        },

        // Shows the browser's install dialog. Returns 'accepted', 'dismissed' or 'unavailable'.
        async prompt() {
            const event = deferredPrompt;
            if (!event) return 'unavailable';

            // The event can only be used once; the browser fires a new one if the player says no.
            deferredPrompt = null;
            await event.prompt();
            const { outcome } = await event.userChoice;
            notify();
            return outcome;
        }
    };
})();
