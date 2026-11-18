// Registers the service worker and tells the app when a new version has been downloaded.
// The new version waits until the player chooses to reload (see UpdateBanner.razor), so the
// running app never mixes old and new files.
(() => {
    let waiting = null;

    window.hackdotUpdates = {
        listener: null,

        // Called by UpdateBanner with a DotNetObjectReference that has an OnUpdateReady method.
        subscribe(listener) {
            this.listener = listener;
            if (waiting) announce(waiting);
        },

        unsubscribe() {
            this.listener = null;
        },

        // Activates the waiting version; 'controllerchange' below then reloads the page. A worker
        // from an older release may not understand the message, so reload anyway after a moment.
        apply() {
            waiting?.postMessage({ type: 'SKIP_WAITING' });
            setTimeout(() => location.reload(), waiting ? 3000 : 0);
        }
    };

    function announce(worker) {
        waiting = worker;
        window.hackdotUpdates.listener?.invokeMethodAsync('OnUpdateReady');
    }

    if (!('serviceWorker' in navigator)) {
        return;
    }

    // Only an update replaces an existing controller; the very first install doesn't need a reload.
    const hadController = !!navigator.serviceWorker.controller;
    let reloading = false;
    navigator.serviceWorker.addEventListener('controllerchange', () => {
        if (hadController && !reloading) {
            reloading = true;
            location.reload();
        }
    });

    navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' }).then(registration => {
        // A worker that finishes installing while another one controls the page is an update.
        const track = worker => worker?.addEventListener('statechange', () => {
            if (worker.state === 'installed' && navigator.serviceWorker.controller) {
                announce(worker);
            }
        });

        if (registration.waiting && navigator.serviceWorker.controller) {
            announce(registration.waiting);
        }
        track(registration.installing);
        registration.addEventListener('updatefound', () => track(registration.installing));

        // An installed app can stay open for days, so look for updates when it comes back into view
        // and once an hour.
        const check = () => registration.update().catch(() => { });
        document.addEventListener('visibilitychange', () => {
            if (document.visibilityState === 'visible') {
                check();
            }
        });
        setInterval(check, 60 * 60 * 1000);
    }).catch(error => console.warn('Service worker registration failed:', error));
})();
