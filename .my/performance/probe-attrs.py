#!/usr/bin/env python3
"""Step 7 probe: does a find return element attributes in one request, and what does it cost?

Usage: probe-attrs.py ios|android
Starts its own Appium session on the app as installed (no mock backend; the first page is enough).
"""
import json, sys, time, urllib.request

BASE = "http://127.0.0.1:4723"
HEAD = sys.argv[1]
ATTRS = sys.argv[2] if len(sys.argv) > 2 else "attribute/name,text,rect,enabled,displayed"


def call(method, path, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method, headers={"Content-Type": "application/json"})
    start = time.perf_counter()
    with urllib.request.urlopen(req, timeout=300) as resp:
        value = json.loads(resp.read())["value"]
    return value, (time.perf_counter() - start) * 1000


if HEAD == "ios":
    caps = {"platformName": "iOS", "appium:automationName": "XCUITest", "appium:udid": "A4395365-6928-480E-BE73-4C04C175420F",
            "appium:bundleId": "com.exact.construction", "appium:noReset": True, "appium:waitForIdleTimeout": 0}
    query = {"using": "-ios class chain", "value": sys.argv[3] if len(sys.argv) > 3 else "**/XCUIElementTypeButton"}
    reads = ["attribute/name", "rect", "enabled", "displayed"]
else:
    caps = {"platformName": "Android", "appium:automationName": "UiAutomator2", "appium:appPackage": "com.exact.construction",
            "appium:appActivity": "com.exact.construction.MainActivity", "appium:noReset": True}
    query = {"using": "-android uiautomator", "value": 'new UiSelector().resourceIdMatches(".+")'}
    reads = ["attribute/resource-id", "text", "rect", "enabled"]

session, ms = call("POST", "/session", {"capabilities": {"alwaysMatch": caps}})
sid = session["sessionId"]
print(f"session {ms:.0f} ms")
time.sleep(8)
try:
    for _ in range(2):
        call("POST", f"/session/{sid}/appium/settings", {"settings": {"shouldUseCompactResponses": True}})
        plain, find_ms = call("POST", f"/session/{sid}/elements", query)
        read_ms = 0.0
        for element in plain[:10]:
            eid = next(iter(element.values()))
            for r in reads:
                _, t = call("GET", f"/session/{sid}/element/{eid}/{r}")
                read_ms += t
        n = min(10, len(plain))
        print(f"compact:   {len(plain)} found in {find_ms:.0f} ms; {len(reads)} reads x {n} elements {read_ms:.0f} ms")

        call("POST", f"/session/{sid}/appium/settings",
             {"settings": {"shouldUseCompactResponses": False, "elementResponseAttributes": ATTRS}})
        rich, rich_ms = call("POST", f"/session/{sid}/elements", query)
        print(f"attributes: {len(rich)} found in {rich_ms:.0f} ms; first: {json.dumps(rich[0])[:300] if rich else '-'}")
finally:
    call("DELETE", f"/session/{sid}")
