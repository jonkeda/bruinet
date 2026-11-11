# AD-005: Physical Input Is Opt-In

Routine actions use semantic control APIs and UI Automation patterns.

**MAUI on Windows uses no physical input at all.** `Bruinet.Maui.FlaUI` has no code that
moves the pointer, types, writes the clipboard or takes the foreground. Every action is a
UI Automation pattern or a verb the app answers through the gesture bridge
([AD-008](ad-008-gestures-and-semantic-actions-go-through-ui-automation.md)), and an
action with neither throws `RouteUnavailableException`, naming the route the app would have to
offer; the call does not retry it, as it would a plain `NotSupportedException` (`Poller.IsFatal`). There is no setting that brings
physical input back, and no test of real input runs on Windows; such a test belongs on the
mobile head, where Appium injects input inside the device.

**WPF and WinForms still fall back to real input**, with no semantic route behind it. There,
real mouse, keyboard, clipboard and foreground-window use pass through one gate,
`PhysicalInput`, controlled by `BRUINET_BACKGROUND_MODE`:

| Value | Physical input |
| --- | --- |
| unset | performed |
| `1`, or any other value | refused: the call throws `PhysicalInputRefusedException` |
| `audit` | performed, and every use recorded (`BRUINET_PHYSICAL_INPUT_LOG` names a file) |
| `0`, `false`, `off`, `allow` | performed |

Refusing by default would fail their suites rather than quieten them. The variable has no
effect on MAUI. Evidence: [the history and the removal plan at d4917d0](https://github.com/exact-mobile-apps/test-tooling/blob/d4917d0/.my/bridge/no-physical-input.md).

**Showing the app is not input.** During a test run the MAUI driver keeps the app behind the person's
windows (`QuietWindow`). `BRUINET_AUT_WINDOW=shown` (`AutWindow`, set by the Presenter) leaves it
where it can be watched; placing it, its z-order and giving the focus back to the Presenter are window
management, not pointer, keyboard or clipboard use. A shown browser is headed. The default stays
`background`, so a test run does not change.

**Broken when:** `Bruinet.Maui.FlaUI` gains pointer/keyboard/clipboard/foreground code, MAUI's element derives from the shared `UiaElementBase` (`Bruinet.FlaUI`, where WPF's and WinForms' fallback lives), or a
setting is added to re-enable physical input on MAUI.
