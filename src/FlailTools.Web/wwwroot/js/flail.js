// The two things a generator page cannot do from C# alone.
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
    }
};
