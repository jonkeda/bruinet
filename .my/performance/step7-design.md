# Step 7: facts with the find

Part of the [performance plan](plan.md), step 7.

## The problem

On iOS every read of an element is one WDA request of about 90 to 240 ms. In the last
iOS short set, `displayed`, `rect`, `attribute/name`, `text` and `enabled` reads took about
260 s of the 778 s. Most of them follow a find in the same attempt: find the element, then
ask if it is shown, enabled, where it is and what it says.

## What the probe showed

Both drivers return chosen facts with each found element when the session has the settings
`shouldUseCompactResponses=false` and `elementResponseAttributes`. The probe is
[probe-attrs.py](probe-attrs.py).

| Find | Plain find, then 4 reads each | Find with 5 facts |
|---|---|---|
| iOS, 21 buttons | 1.8 s, plus about 90 ms per read | 2.1 s, about 10 ms per element |
| Android, 11 elements | 0.04 s, plus about 12 ms per read | 0.2 s, about 15 ms per element |

On iOS the facts cost about one tenth of a read. On Android they cost about as much as one
read, so Android gains little and stays as it is.

## The change

- iOS sessions get the two settings, with the facts `attribute/name`, `text`, `rect`,
  `enabled` and `displayed`.
- `AppiumMauiDriver` sends finds through the public `CommandExecutor`, because the client's
  `FindElements` keeps only the element id. It builds the same `AppiumElement` from the id.
- When a find runs inside an attempt, each found element's facts go into the attempt memo,
  under the same questions its reads use. `Visible`, `Enabled`, `Rect`, `Text` and
  `AutomationId` ask the memo first.
- Outside an attempt nothing is kept, and every read goes to the driver as today.

## Rules it keeps

- A fact lives for the attempt that found the element and no longer (R2). The next attempt
  finds again and gets fresh facts.
- An action ends what is known: a click, a type or a gesture on an element forgets that
  element's facts for the rest of the attempt. The memo gets a `Forget(subject)`.
- A fact is never kept across an action, and no page-source snapshot is read.
- iOS `name` from the find is the element type, so the AutomationId fact is
  `attribute/name`, the same as the read it replaces.

## Risks

Every find in the session pays for the facts, also a find that uses only the first element.
A find of 172 elements cost 1.4 s more. Bruinet's large finds are list rows, which read
these facts anyway. If a run shows a slow find without reads, that find can switch the
setting off for its own call.

The text fact on iOS is the element's value or label, which is what `Text` reads now. The
probe saw `"text": "More actions"` on a button, the same as the read.

## How it is checked

- Bruinet unit tests for the memo seeding and `Forget`.
- The headless set, which does not use Appium, must not change.
- The iOS short set: kept only if it is faster and all 30 pass. One Android class, to show
  that the Android path is unchanged.
