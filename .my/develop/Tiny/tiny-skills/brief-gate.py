import re, sys
# The tiny brief skill's gate: under 500 words, and every scenario in a gherkin block parses (a Feature line and several When and Then allowed).
# python brief-gate.py <brief.md>...
for path in sys.argv[1:]:
    text = open(path, encoding='utf-8').read()
    words = len(text.split())
    blocks = re.findall(r'```gherkin\n(.*?)```', text, re.S)
    problems = []
    scenarios = 0
    for b in blocks:
        for sc in re.split(r'\n\s*(?=Scenario)', '\n' + b):
            lines = [l.strip() for l in sc.strip().splitlines() if l.strip()]
            if not lines: continue
            if lines[0].startswith('Feature') or lines[0].startswith('Background'): lines = [l for l in lines if l.startswith(('Scenario','Given','When','Then','And','But'))]
            if not lines: continue
            if not lines[0].startswith('Scenario'):
                problems.append('text before a Scenario: ' + lines[0][:40]); continue
            scenarios += 1
            order, at, seen = ['Given', 'When', 'Then'], -1, set()
            for l in lines[1:]:
                k = l.split(' ', 1)[0]
                if k in ('And', 'But'):
                    continue
                if k not in order:
                    problems.append(f'{lines[0][:40]}: line "{l[:30]}"'); continue
                if False: problems.append(f'{lines[0][:40]}: {k} after {order[at]}')
                at = max(at, order.index(k)); seen.add(k)
            for k in ('When', 'Then'):
                if k not in seen: problems.append(f'{lines[0][:40]}: no {k}')
    bullets = len(re.findall(r'(?m)^\s*-\s+\*\*[^*]+\*\*\.', text))
    ok = words < 500 and scenarios > 0 and not problems
    print(f'{path.split("tiny2/")[-1]}: {"PASS" if ok else "FAIL"} - {words} words, {scenarios} scenarios in Gherkin' + (f', {len(problems)} problems: ' + '; '.join(problems) if problems else ''))
