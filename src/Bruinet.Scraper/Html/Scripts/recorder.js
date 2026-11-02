// Records what the person does in this frame, for a test recording: clicks, a field's final value, a
// choice, a check, Enter/Escape/Tab. Each action goes to the host through window.__bruinetAct as JSON;
// its element is kept in window.__bruinetPending under the action's id, and the host looks it up in the
// last capture (or captures first). Installed once per document; the host ignores it while not recording.
// A secret field's value never leaves the page: a password field, or one of options.secrets by test id,
// name, id or label; its action carries the placeholder instead.
(options) => {
    const testIds = options.testIds;
    const secrets = options.secrets || [];
    if (window.__bruinetRecorder) return true;
    window.__bruinetRecorder = true;
    const pending = window.__bruinetPending = window.__bruinetPending || {};
    let next = 0;

    const parentOf = e => e.parentElement || (e.getRootNode && e.getRootNode() instanceof ShadowRoot ? e.getRootNode().host : null);
    const textOf = e => (e.innerText || e.textContent || '').replace(/\s+/g, ' ').trim();
    const testIdOf = e => {
        for (const name of testIds) {
            const value = e.getAttribute(name);
            if (value) return value;
        }
        return undefined;
    };
    const pathOf = el => {
        const steps = [];
        for (let e = el; e; e = parentOf(e)) {
            steps.unshift({
                type: e.tagName.toLowerCase(),
                testId: testIdOf(e),
                role: e.getAttribute('role') || undefined,
                name: e.getAttribute('aria-label') || undefined,
                classes: (e.getAttribute('class') || '').trim() || undefined,
            });
        }
        return steps;
    };

    const send = (kind, el, extra) => {
        const action = Object.assign({ kind }, extra);
        if (el) {
            action.id = ++next;
            pending[action.id] = el;
            action.path = pathOf(el);
            // Its number now: a click that navigates leaves no document to look it up in later.
            const i = (window.__bruinetScrapeNodes || []).indexOf(el);
            if (i >= 0) {
                action.local = i + 1;
                action.gen = window.__bruinetScrapeGen;
            }
        }
        window.__bruinetAct(JSON.stringify(action));
    };

    const isToggle = e => e instanceof HTMLInputElement && (e.type === 'checkbox' || e.type === 'radio');
    const isButtonInput = e => e instanceof HTMLInputElement && ['button', 'submit', 'reset', 'image'].includes(e.type);
    const isText = e => e instanceof HTMLTextAreaElement
        || (e instanceof HTMLInputElement && !isToggle(e) && !isButtonInput(e) && !['file', 'hidden', 'range', 'color'].includes(e.type));
    // A field records its own change; a click into it is not an action of its own.
    const isField = e => (e instanceof HTMLInputElement && !isButtonInput(e)) || e instanceof HTMLTextAreaElement || e instanceof HTMLSelectElement;
    const clickable = 'a,button,summary,label,input,select,textarea,[role=button],[role=link],[role=tab],[role=menuitem],'
        + '[role=checkbox],[role=switch],[role=radio],[role=option],[onclick]';

    // The element a list box belongs to: the combobox that controls or owns it, else the list box.
    const ownerOf = list => {
        if (list.id) {
            const owner = document.querySelector(`[aria-controls~="${CSS.escape(list.id)}"],[aria-owns~="${CSS.escape(list.id)}"]`);
            if (owner) return owner;
        }
        return list;
    };

    // A custom checkbox flips aria-checked after the click, sometimes after a round trip to the server.
    const watchChecked = el => {
        const before = el.getAttribute('aria-checked');
        const observer = new MutationObserver(() => {
            const now = el.getAttribute('aria-checked');
            if (now === before) return;
            observer.disconnect();
            send('check', el, { checked: now === 'true' });
        });
        observer.observe(el, { attributes: true, attributeFilter: ['aria-checked'] });
        setTimeout(() => observer.disconnect(), 5000);
    };

    // The value last sent for a field, or its value when it got focus: a change back to it is no action.
    const sent = new WeakMap();
    const same = (a, b) => !!a && !!b && a.trim().toLowerCase() === b.trim().toLowerCase();
    const secretOf = el => {
        const labels = el.labels ? [...el.labels].map(textOf) : [];
        const keys = [testIdOf(el), el.getAttribute('name'), el.id, ...labels];
        const secret = secrets.find(s => keys.some(k => same(k, s.field)));
        if (secret) return secret.placeholder;
        return el instanceof HTMLInputElement && el.type === 'password' ? 'password' : null;
    };
    const valueOf = (el, value) => {
        const secret = secretOf(el);
        return secret ? { secret } : { value };
    };
    const sendValue = el => {
        if (sent.get(el) === el.value) return;
        sent.set(el, el.value);
        send('input', el, valueOf(el, el.value));
    };

    // The toolbar's own clicks, a click that picks an element for an assert, and the keys of choosing its check
    // (Escape cancels it) are not the person's actions.
    const ours = ev => !!window.__bruinetPickCancel || !!window.__bruinetChooseCancel || ev.composedPath().some(e => e.id === '__bruinet_toolbar');

    document.addEventListener('click', ev => {
        if (ours(ev)) return;
        let el = ev.composedPath()[0];
        if (!(el instanceof Element)) return;
        if (el instanceof SVGElement) while (el.ownerSVGElement) el = el.ownerSVGElement; // an icon is one node
        el = el.closest(clickable) || el;
        if (el instanceof HTMLLabelElement && el.control) el = el.control;
        if (isField(el)) return;
        const option = el.closest('[role=option]');
        const list = option && option.closest('[role=listbox]');
        if (list) {
            send('select', ownerOf(list), { value: textOf(option) });
            return;
        }
        if (['checkbox', 'switch', 'radio'].includes(el.getAttribute('role'))) watchChecked(el);
        send('click', el);
    }, true);

    document.addEventListener('focusin', ev => {
        if (ours(ev)) return;
        const el = ev.composedPath()[0];
        if (isText(el)) sent.set(el, el.value);
    }, true);

    document.addEventListener('change', ev => {
        if (ours(ev)) return;
        const el = ev.composedPath()[0];
        if (isToggle(el)) send('check', el, { checked: el.checked });
        else if (el instanceof HTMLSelectElement) send('select', el, valueOf(el, el.selectedOptions.length ? textOf(el.selectedOptions[0]) : ''));
        else if (isText(el)) sendValue(el);
    }, true);

    document.addEventListener('keydown', ev => {
        if (!['Enter', 'Escape', 'Tab'].includes(ev.key) || ev.repeat || ours(ev)) return;
        const el = ev.composedPath()[0];
        if (ev.key === 'Enter' && el instanceof HTMLTextAreaElement) return; // a new line, part of the value
        if (isText(el)) sendValue(el); // the value comes before the key that submits or leaves it
        send('key', null, { value: ev.key });
    }, true);

    return true;
}
