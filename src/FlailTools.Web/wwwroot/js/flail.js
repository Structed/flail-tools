// The handful of things the pages cannot do from C# alone.
window.flailTools = {
    // Replaces the address bar without navigating, so the link always describes what is on screen
    // without pushing a history entry per button press.
    setUrl: function (url) {
        history.replaceState(null, '', url);
    },

    copyText: async function (text) {
        if (!navigator.clipboard) {
            return false;
        }

        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            return false;
        }
    },

    download: function (name, text, type) {
        const blob = new Blob([text], { type: type || 'application/json' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');

        link.href = url;
        link.download = name;
        document.body.appendChild(link);
        link.click();
        link.remove();

        // Revoked on the next tick, so the click has taken the URL first.
        setTimeout(() => URL.revokeObjectURL(url), 0);
    },

    // Remembering what someone called themselves at the table, and which table they were at.
    //
    // Guarded because storage is not always there to be written to: private windows, storage
    // quotas, and browsers set to refuse it all throw from the same call. A dice roller that will
    // not open because it could not remember a name would be a poor trade.
    read: function (key) {
        try {
            return localStorage.getItem(key);
        } catch {
            return null;
        }
    },

    write: function (key, value) {
        try {
            if (value === null || value === undefined || value === '') {
                localStorage.removeItem(key);
            } else {
                localStorage.setItem(key, value);
            }

            return true;
        } catch {
            return false;
        }
    },

    // The problem report.
    //
    // The address is read off the browser rather than the router, because the pages rewrite it with
    // replaceState and the router is never told. What may be published of it is decided in C#.
    pageUrl: function () {
        return location.href;
    },

    describeBrowser: function () {
        return {
            userAgent: navigator.userAgent || '',
            viewport: `${window.innerWidth} x ${window.innerHeight}`
        };
    },

    showDialog: function (dialog) {
        if (dialog && !dialog.open) {
            dialog.showModal();
        }
    },

    closeDialog: function (dialog) {
        if (dialog && dialog.open) {
            dialog.close();
        }
    },

    forget: function (url) {
        if (url) {
            URL.revokeObjectURL(url);
        }
    },

    canCaptureTab: function () {
        return !!(navigator.mediaDevices && navigator.mediaDevices.getDisplayMedia);
    },

    // One picture of the tab, for the problem report.
    //
    // Taken by asking the browser to share the tab rather than by redrawing the page, because the
    // browser already knows exactly what is on screen and a library that re-renders the DOM would
    // be a dependency paid for to get it slightly wrong. The price is a permission prompt, and no
    // support on phones, where the button is not offered.
    //
    // The dialog is closed first so it is not in the picture, and the page is marked as capturing
    // so anything it holds private — a dice table code — is hidden for the moment the frame is
    // taken. The sharing stops as soon as there is a frame.
    //
    // GitHub takes images only by paste or drop, so the picture goes on the clipboard, or into the
    // downloads where the clipboard refuses it, and the reporter carries it across themselves.
    captureTab: async function (dialog) {
        if (!window.flailTools.canCaptureTab()) {
            return { outcome: 'failed' };
        }

        const root = document.documentElement;
        let stream = null;
        let picture = null;
        let outcome = 'failed';

        if (dialog && dialog.open) {
            dialog.close();
        }

        root.classList.add('capturing');

        try {
            stream = await navigator.mediaDevices.getDisplayMedia({
                video: { displaySurface: 'browser' },
                audio: false,
                preferCurrentTab: true,
                selfBrowserSurface: 'include',
                surfaceSwitching: 'exclude'
            });

            picture = await grabFrame(stream);
        } catch (error) {
            // Declining the prompt and a policy refusing it both arrive as NotAllowedError.
            outcome = error && error.name === 'NotAllowedError' ? 'cancelled' : 'failed';
        } finally {
            if (stream) {
                stream.getTracks().forEach(track => track.stop());
            }

            root.classList.remove('capturing');

            if (dialog && !dialog.open) {
                dialog.showModal();
            }
        }

        if (!picture) {
            return { outcome: outcome };
        }

        const preview = URL.createObjectURL(picture);

        if (await copyImage(picture)) {
            return { outcome: 'copied', preview: preview };
        }

        window.flailTools.download('flail-tools-screenshot.png', picture, 'image/png');

        return { outcome: 'downloaded', preview: preview };

        async function grabFrame(source) {
            const video = document.createElement('video');

            video.muted = true;
            video.playsInline = true;
            video.srcObject = source;
            await video.play();

            // The first frame can predate the repaint that took the dialog away, so wait a moment
            // and then for a fresh one. A still page may never send another, hence the race.
            await new Promise(resolve => setTimeout(resolve, 250));

            if (video.requestVideoFrameCallback) {
                await Promise.race([
                    new Promise(resolve => video.requestVideoFrameCallback(() => resolve())),
                    new Promise(resolve => setTimeout(resolve, 1000))
                ]);
            }

            if (!video.videoWidth || !video.videoHeight) {
                return null;
            }

            const canvas = document.createElement('canvas');

            canvas.width = video.videoWidth;
            canvas.height = video.videoHeight;
            canvas.getContext('2d').drawImage(video, 0, 0);
            video.pause();
            video.srcObject = null;

            return await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
        }

        async function copyImage(image) {
            if (!navigator.clipboard || !navigator.clipboard.write || typeof ClipboardItem === 'undefined') {
                return false;
            }

            try {
                await navigator.clipboard.write([new ClipboardItem({ 'image/png': image })]);
                return true;
            } catch {
                // Commonly a document that has not got its focus back from the sharing prompt.
                return false;
            }
        }
    },

    // Where the dice table's signalling actually stands, relay by relay.
    //
    // The channel and the transport under it both ship in Structed.Inkwell.Party.Blazor and neither
    // is reimplemented here. The relays are named in C# (SignallingRelays), but which ones are in
    // play is still read back off the transport rather than off that list: what the check reports
    // should be what the page is actually holding, not what it was asked to hold, and the two would
    // drift the first time either end changed.
    //
    // The module is addressed against the document base on purpose. party.js imports it as
    // './trystero-nostr.js' from its own folder under _content/, and the module map is keyed by
    // resolved URL — arrive at a different spelling of the same file and the import succeeds, hands
    // back a second copy with no sockets in it, and the check cheerfully reports nothing at all.
    //
    // Asking each relay again, directly, is the half that earns this its place. A relay that
    // answers a fresh socket while the transport's is shut is one this page has given up on and
    // will not retry; a relay that answers neither is being stopped before it leaves the machine.
    // The two look the same on screen and want opposite things done about them.
    relays: async function (patience) {
        const wait = patience > 0 ? patience : 8000;
        let sockets;

        try {
            const module = new URL(
                '_content/Structed.Inkwell.Party.Blazor/js/trystero-nostr.js',
                document.baseURI).href;

            sockets = (await import(module)).getRelaySockets();
        } catch {
            // No transport loaded, so there is nothing to report and nothing to guess at.
            return null;
        }

        const probe = url => new Promise(resolve => {
            let socket = null;

            const settle = state => {
                clearTimeout(timer);

                try {
                    socket?.close();
                } catch {
                    // Never opened, or already shut. Either way there is nothing to close.
                }

                resolve(state);
            };

            const timer = setTimeout(() => settle('blocked'), wait);

            try {
                socket = new WebSocket(url);
            } catch {
                settle('blocked');
                return;
            }

            socket.onopen = () => settle('reachable');
            socket.onerror = () => settle('blocked');
        });

        const urls = Object.keys(sockets);

        // Gathered into the original order rather than the order they finished in, so the list does
        // not reshuffle itself between one press of the button and the next.
        const states = await Promise.all(urls.map(url =>
            sockets[url] && sockets[url].readyState === 1 ? 'open' : probe(url)));

        const found = {};

        urls.forEach((url, index) => {
            found[url] = states[index];
        });

        return found;
    }
};
