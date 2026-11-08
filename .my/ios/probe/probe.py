#!/usr/bin/env python3
"""Raw Appium probe without Bruinet: opens the Lists gallery and times every WebDriver call.

Usage: probe.py <tap-mode> [settings-json]
  tap-mode: click (element click), tap (mobile: tap at centre), actions (W3C pointer)
"""
import json, sys, time, urllib.request, os

APPIUM = "http://127.0.0.1:4723"
APP = os.path.expanduser("~/Documents/MainConstruction/construction/Exact.Construction/bin/Debug/net10.0-ios/iossimulator-arm64/Exact.Construction.app")
UDID = "A4395365-6928-480E-BE73-4C04C175420F"
mode = sys.argv[1] if len(sys.argv) > 1 else "click"
settings = json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}
t0 = time.time()
log = []

def call(method, path, body=None, label=""):
    start = time.time()
    req = urllib.request.Request(APPIUM + path, method=method,
                                 data=None if body is None else json.dumps(body).encode(),
                                 headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=300) as r:
            out = json.loads(r.read() or b"{}")
    except urllib.error.HTTPError as e:
        out = json.loads(e.read() or b"{}")
    ms = int((time.time() - start) * 1000)
    log.append((round(time.time() - t0, 1), ms, label or path.split("/")[-1]))
    print(f"{time.time()-t0:7.1f}s {ms:6} ms  {label or method + ' ' + path[-40:]}", flush=True)
    return out.get("value")

caps = {"platformName": "iOS", "appium:app": APP, "appium:automationName": "XCUITest",
        "appium:deviceName": "iPhone 17", "appium:platformVersion": "27.0", "appium:udid": UDID,
        "appium:waitForQuiescence": False, "appium:settings[waitForIdleTimeout]": 1,
        "appium:processArguments": {"env": {"CONSTRUCTION_USE_MOCK_BACKEND": "true", "MOCK_SERVER_PORT": "9090"}}}
caps.update({k: v for k, v in settings.items() if k.startswith("appium:")})
r = call("POST", "/session", {"capabilities": {"firstMatch": [caps]}}, "new session")
sid = r["sessionId"]
S = f"/session/{sid}"
plain = {k: v for k, v in settings.items() if not k.startswith("appium:")}
if plain:
    call("POST", S + "/appium/settings", {"settings": plain}, f"settings {plain}")

def find(aid, timeout=30):
    end = time.time() + timeout
    while time.time() < end:
        v = call("POST", S + "/elements", {"using": "accessibility id", "value": aid}, f"find {aid}")
        if v:
            return list(v[0].values())[0]
        time.sleep(0.25)
    raise SystemExit(f"not found: {aid}")

def tap(el, label):
    if mode == "click":
        call("POST", f"{S}/element/{el}/click", {}, f"CLICK {label}")
        return
    r = call("GET", f"{S}/element/{el}/rect", None, "rect")
    x, y = r["x"] + r["width"] // 2, r["y"] + r["height"] // 2
    if mode == "tap":
        call("POST", S + "/execute/sync", {"script": "mobile: tap", "args": [{"x": x, "y": y}]}, f"TAP {label}")
    else:
        call("POST", S + "/actions", {"actions": [{"type": "pointer", "id": "f", "parameters": {"pointerType": "touch"},
            "actions": [{"type": "pointerMove", "duration": 0, "x": x, "y": y}, {"type": "pointerDown", "button": 0},
                        {"type": "pause", "duration": 50}, {"type": "pointerUp", "button": 0}]}]}, f"ACTIONS {label}")

# Same path as ListsGalleryTests.OpenLists: the More tab, Developer, Control gallery, Lists.
root = find("TabMenuView", 120)
end = time.time() + 60
while time.time() < end:
    grids = call("POST", f"{S}/element/{root}/elements", {"using": "accessibility id", "value": "TabMenuView_Grid"}, "find grids") or []
    if len(grids) >= 5:
        break
    time.sleep(0.5)
tap(list(grids[-1].values())[0], "More tab")
cells = None
end = time.time() + 30
while time.time() < end:
    caps_ = call("POST", S + "/elements", {"using": "accessibility id", "value": "MenuItemCell_Caption"}, "find captions")
    texts = [call("GET", f"{S}/element/{list(c.values())[0]}/text", None, "text") for c in caps_ or []]
    if any(t and "evelop" in t for t in texts):
        cells = caps_
        idx = [i for i, t in enumerate(texts) if t and "evelop" in t][0]
        break
    time.sleep(0.25)
tap(list(cells[idx].values())[0], "Developer")
tap(find("OpenControlGalleryButton"), "Control gallery")
tap(find("Gallery_Open_Lists"), "Lists")
lst = find("Gallery_Lists_List")
# Scroll to row 40 with drags, reading titles each step.
seen = set()
for step in range(30):
    rows = call("POST", f"{S}/element/{lst}/elements", {"using": "accessibility id", "value": "Title"}, "find titles") or []
    for t in rows:
        seen.add(call("GET", f"{S}/element/{list(t.values())[0]}/text", None, "text"))
    if "Item 40" in seen:
        break
    call("POST", S + "/execute/sync", {"script": "mobile: dragFromToForDuration",
         "args": [{"duration": 0.1, "fromX": 201, "fromY": 760, "toX": 201, "toY": 260}]}, "DRAG")
print("found Item 40" if "Item 40" in seen else f"NOT found; seen {sorted(seen)[-3:]}")
call("DELETE", S, None, "delete session")
slow = [x for x in log if x[1] > 1000]
print(f"TOTAL {time.time()-t0:.1f}s, {len(log)} calls, {sum(x[1] for x in log)/1000:.1f}s in calls; slow: {slow}")
