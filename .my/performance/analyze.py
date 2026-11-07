#!/usr/bin/env python3
"""Summarises one measured run: Appium requests in a log slice, and test seconds from trx files.

Usage: analyze.py <appium-log-slice> <label> [trx ...]
Prints one block and appends a TSV line to results.tsv next to this script.
"""
import re, sys, os, collections, xml.etree.ElementTree as ET

log, label, trxs = sys.argv[1], sys.argv[2], sys.argv[3:]
line_re = re.compile(r"<-- (GET|POST|DELETE) /session(?:/[^/ ]+)?(\S*) (\d{3}) (\d+) ms")
by = collections.defaultdict(lambda: [0, 0])
total = slow = 0
slow_ms = 0
for line in open(log, errors="replace"):
    m = line_re.search(line)
    if not m:
        continue
    verb, path, ms = m.group(1), m.group(2), int(m.group(4))
    path = re.sub(r"/element/[^/]+", "/element/:id", path) or "/"
    key = f"{verb} {path}" if path != "/" or verb != "POST" else "POST session"
    by[key][0] += 1
    by[key][1] += ms
    total += 1
    if ms > 1000:
        slow += 1
        slow_ms += ms

test_s = 0.0
passed = failed = 0
for t in trxs:
    root = ET.parse(t).getroot()
    for r in root.iter():
        if r.tag.endswith("UnitTestResult"):
            h, mnt, s = r.get("duration", "0:0:0").split(":")
            test_s += int(h) * 3600 + int(mnt) * 60 + float(s)
            if r.get("outcome") == "Passed":
                passed += 1
            else:
                failed += 1

req_ms = sum(v[1] for v in by.values())
print(f"== {label}: tests {passed} passed {failed} failed, test {test_s:.0f} s, "
      f"requests {total} ({req_ms/1000:.0f} s), over 1 s: {slow} ({slow_ms/1000:.0f} s)")
for k, (n, ms) in sorted(by.items(), key=lambda kv: -kv[1][1])[:15]:
    print(f"  {n:6d} {ms/1000:8.1f} s  {k}")

out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "results.tsv")
new = not os.path.exists(out)
with open(out, "a") as f:
    if new:
        f.write("label\tpassed\tfailed\ttest_s\trequests\trequest_s\tover1s\tover1s_s\trect\tenabled\tdisplayed\n")
    get = lambda suffix: sum(v[0] for k, v in by.items() if k.endswith(suffix))
    f.write(f"{label}\t{passed}\t{failed}\t{test_s:.0f}\t{total}\t{req_ms/1000:.0f}\t{slow}\t{slow_ms/1000:.0f}"
            f"\t{get('/rect')}\t{get('/enabled')}\t{get('/displayed')}\n")
