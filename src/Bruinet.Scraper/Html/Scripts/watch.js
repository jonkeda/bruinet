// The Recorder's watcher, in the page's top document: tells the host of each press (with the page as it is at that
// moment) and of the page going still after one. Off until the host turns it on; it never reads a key.
(config) => {
    if (window.top !== window || window.__bruinetRecorder) return;
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

    // What was pressed: the nearest control around the click, else a row, else what shows a hand.
    function pressed(path) {
        const elements = path.filter(node => node instanceof Element);
        for (const el of elements) {
            const role = roleOf(el);
            if (role) return { el, role };
        }
        const row = elements.find(el => el.tagName === 'TR' || el.getAttribute('role') === 'row');
        if (row) return { el: row, role: 'row' };
        const hand = elements.filter(el => getComputedStyle(el).cursor === 'pointer');
        return hand.length ? { el: hand[hand.length - 1], role: 'clickable' } : null;
    }

    function nameOf(el) {
        const text = el.getAttribute('aria-label') || el.innerText || el.value || el.getAttribute('title')
            || (el.querySelector && el.querySelector('img[alt]') || { alt: '' }).alt || '';
        return String(text).replace(/\s+/g, ' ').trim().substring(0, 80);
    }

    window.addEventListener('click', event => {
        if (!on || !event.isTrusted) return;
        const found = pressed(event.composedPath());
        if (!found) return;
        const capture = take();
        const node = (window.__bruinetScrapeNodes || []).indexOf(found.el) + 1;
        post({ kind: 'press', name: nameOf(found.el), role: found.role, node, capture });
        step();
    }, true);

    new MutationObserver(changed).observe(document, { subtree: true, childList: true, attributes: true, characterData: true });
    for (const name of ['load', 'popstate', 'hashchange', 'pageshow'])
        window.addEventListener(name, step);

    // The calls the page waits for, counted: a page that fetches its data is not still while it waits.
    const done = () => { pending = Math.max(0, pending - 1); changed(); };
    if (window.fetch) {
        const fetch = window.fetch;
        window.fetch = function () {
            pending++;
            const answer = fetch.apply(this, arguments);
            answer.then(done, done);
            return answer;
        };
    }
    const send = XMLHttpRequest.prototype.send;
    XMLHttpRequest.prototype.send = function () {
        pending++;
        this.addEventListener('loadend', done, { once: true });
        return send.apply(this, arguments);
    };

    window.__bruinetRecorder = {
        set(nowOn, nowValues) {
            const started = nowOn && !on;
            on = nowOn;
            values = nowValues;
            if (started) step();
            return 'ok';
        },
    };
    post({ kind: 'hello' });
}
