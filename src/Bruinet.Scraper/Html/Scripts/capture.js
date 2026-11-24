// One frame's document as a flat list of nodes in document order (numbers local to the frame).
// The host captures each frame on its own: every <iframe> is kept in a WeakMap with its number, so
// the host can hang the frame's nodes under it without changing the page. Open shadow roots are nodes
// of their own. What a user typed (value) is never read, unless options.values says so (the Recorder's switch).
// With options.inline a frame of the page's own site is read in place, under its <iframe>.
(options) => {
    const skip = new Set(['HEAD', 'SCRIPT', 'STYLE', 'NOSCRIPT', 'TEMPLATE', 'LINK', 'META']);
    const keptNames = new Set(options.kept);
    const frames = window.__bruinetScrapeFrames = new WeakMap();
    const elements = window.__bruinetScrapeNodes = [];
    window.__bruinetScrapeGen = options.gen; // which capture the numbers are of, for the recorder
    const nodes = [];

    // Vue's scoped-style markers (data-v-1a2b3c4d) change with every build and say nothing about the element.
    const scoped = /^data-v-[0-9a-f]{8}$/;

    function kept(name) {
        return (name.startsWith('data-') && !scoped.test(name)) || (name.startsWith('aria-') && name !== 'aria-label') || keptNames.has(name);
    }

    function ownText(el) {
        let text = '';
        for (const child of el.childNodes)
            if (child.nodeType === 3) text += child.textContent;
        text = text.replace(/\s+/g, ' ').trim();
        return text ? text.substring(0, options.maxText) : null;
    }

    // A field's value as it is now: what the element holds, not what its HTML said.
    function addValue(el, attr) {
        const tag = el.tagName;
        if (tag === 'INPUT' && (el.type === 'checkbox' || el.type === 'radio')) {
            if (el.checked) attr.checked = ''; else delete attr.checked;
            return;
        }
        const field = tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT';
        const own = tag.includes('-') && ['string', 'number', 'boolean'].includes(typeof el.value);
        if (field || own) attr.value = String(el.value).substring(0, options.maxText);
    }

    function add(node, target) {
        node.n = nodes.length + 1;
        nodes.push(node);
        elements.push(target);
        return node.n;
    }

    function capture(el, parent) {
        if (skip.has(el.tagName) || el.id === '__bruinet_highlight' || el.id === '__bruinet_toolbar') return;
        const rect = el.getBoundingClientRect();
        const attr = {};
        for (const a of el.attributes)
            if (kept(a.name)) attr[a.name] = a.value.substring(0, options.maxText);
        if (options.values) addValue(el, attr);
        const node = {
            p: parent,
            type: el.tagName.toLowerCase(),
            role: el.getAttribute('role'),
            name: el.getAttribute('aria-label'),
            text: el instanceof SVGElement ? null : ownText(el),
            box: rect.width || rect.height ? [Math.round(rect.x), Math.round(rect.y), Math.round(rect.width), Math.round(rect.height)] : null,
            attr: Object.keys(attr).length ? attr : null,
        };
        const number = add(node, el);
        if (el.tagName === 'IFRAME' || el.tagName === 'FRAME') {
            frames.set(el, number);
            if (options.inline) {
                let inner = null;
                try { inner = el.contentDocument; } catch { /* another site's frame: not ours to read */ }
                if (inner && inner.documentElement) {
                    node.frame = inner.location.href;
                    capture(inner.documentElement, number);
                }
            }
            return;
        }
        if (el instanceof SVGElement) return; // an icon is one node, not its paths
        if (el.shadowRoot) {
            const root = add({ p: number, kind: 'shadow-root' }, el.shadowRoot);
            for (const child of el.shadowRoot.children) capture(child, root);
        }
        for (const child of el.children) capture(child, number);
    }

    capture(document.documentElement, 0);
    return JSON.stringify({ location: location.href, title: document.title, nodes });
}
