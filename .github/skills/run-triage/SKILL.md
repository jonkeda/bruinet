---
name: run-triage
description: Classify each failing UI test of a run - test bug, site changed, app bug or environment - from its message, Bruinet's artifacts (screenshot, step log, trace) and a rerun, then fix test bugs, send changed pages back to Explore, and write app bugs up for a person. Use when UI tests fail on a shared environment, when asked to triage, investigate or explain test failures, or for loop stage 7 (Triage).
---

# Triage a run

Stage 7 of the site loop. Input: the run's results (`TestResults/*.trx`) and Bruinet's artifacts
(`TestResults/<run>/<test>/`: screenshot, step log, trace). Output: one line per failure in the
run's report with its class and what was done.

## Classes

| Class | Signs | What to do |
| --- | --- | --- |
| Test bug | The app is right and the test asked the wrong thing: a stale assertion, a wrong expected value, a missing wait, the wrong account | Fix the test, rerun it alone, back to stage 6 |
| Site changed | A locator finds nothing or the wrong thing; the page looks different in the screenshot | `scrape verify` the module; on a miss back to stage 2 (Explore), the redraft is a diff for a person |
| App bug | The app does what the spec says it must not, reproducibly | Stays a failure (never weakened or skipped). Write `.testing/issues/<date>-<test>.md`: steps, expected (the rule), seen, screenshot. 👤 |
| Environment | acc down, sign-in refused, a timeout on every test, a notification or slow load covering the page | Rerun once; if it stays, note it in the report and stop the run |
| Spec | The app's behaviour is plausible and the spec assumed otherwise | Back to the spec's owner as an open question; the rule waits |

## Steps

1. Read the failure message first: Bruinet's messages name the element, the reason and what was read.
2. Look at the screenshot and the step log before guessing.
3. Rerun the one test (`--filter FullyQualifiedName~<name>`). Passes now: a flake. Name its cause
   (a wait, a toast, data from another test); a flake is a test bug until shown otherwise.
4. Classify, act as in the table, and add a `friction-log` row when the cause will hit again.
