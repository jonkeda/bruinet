// The Recorder's watcher, in every document of the page: tells the host of each press (with the page as it is at
// that moment) and of the page going still after one. The top document decides and posts; a frame of the same site
// (an app that shows its pages in a frame) hands its clicks and changes to it. Off until the host turns it on; it
// never reads a key.
(config) => {
    if (window.__bruinetRecorder) return;

    // What a document tells the one that decides: a click, a change, a step (it loaded, its address changed), and
    // the calls it waits for.
    function watch(hub) {
        let waiting = 0;
        window.addEventListener('click', event => { if (event.isTrusted) hub().click(event.composedPath()); }, true);
        new MutationObserver(() => hub().changed()).observe(document, { subtree: true, childList: true, attributes: true, characterData: true });
        for (const name of ['load', 'popstate', 'hashchange', 'pageshow'])
            window.addEventListener(name, () => hub().step());
        // A document that goes takes the calls it waited for with it.
        window.addEventListener('pagehide', () => { for (; waiting > 0; waiting--) hub().done(); });

        const began = () => { waiting++; hub().began(); };
        const done = () => { if (waiting > 0) { waiting--; hub().done(); } };
        if (window.fetch) {
            const fetch = window.fetch;
            window.fetch = function () {
                began();
                const answer = fetch.apply(this, arguments);
                answer.then(done, done);
                return answer;
            };
        }
        const send = XMLHttpRequest.prototype.send;
        XMLHttpRequest.prototype.send = function () {
            began();
            this.addEventListener('loadend', done, { once: true });
            return send.apply(this, arguments);
        };
    }

    if (window.top !== window) {
        // A frame: of the same site, it reports to the top document's watcher; of another site, it is not ours to read.
        const idle = { click() { }, changed() { }, step() { }, began() { }, done() { } };
        const hub = () => {
            try { return (window.top.__bruinetRecorder || {}).hub || idle; } catch { return idle; }
        };
        try { void window.top.document; } catch { return; }
        window.__bruinetRecorder = { frame: true };
        watch(hub);
        hub().step(); // a frame that opened another page is a step of the page
        return;
    }

    let on = false, values = false, gen = 0, timer = null, pending = 0, since = 0, until = 0;

    const post = message => config.post(JSON.stringify(message));
    const take = () => config.capture({ kept: config.kept, maxText: config.maxText, gen: ++gen, values, inline: true });

    function still() {
        timer = null;
        if (!on || !document.documentElement) return;
        // Calls the page still waits for an answer to: it is not still yet, unless that takes too long.
        if (pending > 0 && Date.now() - since < config.max) { timer = setTimeout(still, config.quiet); return; }
        since = 0;
        post({ kind: 'still', capture: take() });
    }

    // The page changed: it is still once nothing changed for a while. Only after a step, so a page that
    // never stops changing (a clock) is not captured for ever.
    function changed() {
        if (!on || Date.now() > until) return;
        if (!since) since = Date.now();
        clearTimeout(timer);
        timer = setTimeout(still, config.quiet);
    }

    function step() {
        until = Date.now() + config.max;
        since = 0;
        changed();
    }

    const controls = new Set(['BUTTON', 'SUMMARY', 'OPTION']);
    const inputs = new Set(['button', 'submit', 'reset', 'image', 'checkbox', 'radio']);
    const roles = new Set(['button', 'link', 'tab', 'menuitem', 'menuitemcheckbox', 'menuitemradio', 'option', 'switch', 'checkbox', 'radio', 'treeitem']);

    function roleOf(el) {
        const role = el.getAttribute('role');
        if (role && roles.has(role)) return role;
        if (el.tagName === 'A' && el.hasAttribute('href')) return 'link';
        if (controls.has(el.tagName)) return el.tagName === 'OPTION' ? 'option' : 'button';
        if (el.tagName === 'INPUT' && inputs.has(el.type)) return el.type === 'checkbox' || el.type === 'radio' ? el.type : 'button';
        return null;
    }

    // What was pressed: the nearest control around the click, else a row, else what shows a hand. An element of a
    // frame is one of that frame's own kind, so it is told by what it has, not by what it is an instance of.
    function pressed(path) {
        const elements = path.filter(node => node && node.nodeType === 1);
        for (const el of elements) {
            const role = roleOf(el);
            if (role) return { el, role };
        }
        const row = elements.find(el => el.tagName === 'TR' || el.getAttribute('role') === 'row');
        if (row) return { el: row, role: 'row' };
        const hand = elements.filter(el => el.ownerDocument.defaultView.getComputedStyle(el).cursor === 'pointer');
        return hand.length ? { el: hand[hand.length - 1], role: 'clickable' } : null;
    }

    function nameOf(el) {
        const text = el.getAttribute('aria-label') || el.innerText || el.value || el.getAttribute('title')
            || (el.querySelector && el.querySelector('img[alt]') || { alt: '' }).alt || '';
        return String(text).replace(/\s+/g, ' ').trim().substring(0, 80);
    }

    const hub = {
        click(path) {
            if (!on) return;
            const found = pressed(path);
            if (!found) return;
            const capture = take();
            const node = (window.__bruinetScrapeNodes || []).indexOf(found.el) + 1;
            post({ kind: 'press', name: nameOf(found.el), role: found.role, node, capture });
            step();
        },
        changed,
        step,
        began() { pending++; },
        done() { pending = Math.max(0, pending - 1); changed(); },
    };

    window.__bruinetRecorder = {
        hub,
        set(nowOn, nowValues) {
            const started = nowOn && !on;
            on = nowOn;
            values = nowValues;
            if (started) step();
            return 'ok';
        },
    };
    watch(() => hub);
    post({ kind: 'hello' });
}
