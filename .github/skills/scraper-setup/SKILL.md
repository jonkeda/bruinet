---
name: scraper-setup
description: Set the Scraper up for one app before it runs by itself - ask a person six questions (is the environment safe to write on, everything or a part, may it improve by itself, when may it ask, may it commit, who accepts a spec), write the answers into the app's own Scraper project as setup.md, cut the app into modules without walking it, and let the person choose what goes first. Use when asked to set up, configure or start the Scraper or the factory for an app, to point the Scraper at an application, or before the first `bruinet scrape factory` run on a suite.
---

# The Scraper's setup for one app

The Scraper runs by itself only inside what a person answered first. This skill asks, writes the answers down, and
cuts the app into modules. It runs once per app; an answer is changed later by editing `setup.md`.

Read first: the Scraper's guide (`docs/guides/scraper.md`), and the suite's own `AGENTS.md`: its Never list holds
whatever the answers are.

## Before asking

1. The app has a suite and the Scraper's own project beside it (`<App>.Scraper`, the `site-setup` skill). From that
   project's folder: `bruinet scrape check` answers, signed in.
2. `bruinet scrape factory setup` prints the answers there are. Ask only what has none, or what the person wants
   changed.

## The six questions

Ask them together, each with its proposal. Never ask for a password or a token.

| # | Ask | The answers | With no answer |
| --- | --- | --- | --- |
| 1 | Is this environment safe to write on? Which hosts, which accounts, and what must never be pressed? | `read-only`: links and tabs only, every write held back. `own-records`: writes only on records the Scraper made itself. `free`: anything but what sends, pays or signs out | `read-only` |
| 2 | Everything, or a part? | `all`, or modules by name in their order | Nothing runs until the person chose from `modules.md` |
| 3 | May it improve by itself, and what may such a change touch? | `off`, `data` (the app's signs, holders, templates), `suite` (also the suite's code), `bruinet` (also Bruinet's own) | `data` |
| 4 | When may it ask you something? | `never`, `start`, `batch`, `blocked` | `batch` |
| 5 | May it commit what it kept? | `no`, `batch`, `group`. It never pushes | `no` |
| 6 | Who accepts a spec it drafted? | `person`, `agent` | `person` |

Propose the careful answer when the person is not sure. An environment is `free` only on the person's own word
that it is an acceptance or test environment; a production environment is `read-only`, whatever else is said.

## Write the answers

```powershell
bruinet scrape factory setup --set Writes=own-records --set Run=all --set Improve=data --set Ask=batch --set Commit=no --set Specs=person
```

The hosts, the accounts and what is never pressed are the guards of `scraper.config.md` (`## Explore hosts`,
`## Never click`, `Environment` under `## App`): write those there, with `bruinet scrape config`.

## Cut the app into modules

```powershell
bruinet scrape places seed
bruinet scrape places routes
bruinet scrape factory modules --write
```

`seed` and `routes` fill the list of places from the map, the suite and the routes the app's own scripts name;
nothing is pressed. `modules --write` writes `modules.md`: each module with its groups, pages known, kinds and what
it owes, in the tool's order (plain kinds first, small before large).

Show the person the table. They may change `Order` and `Do` (`yes`, `later`, `no`) in the file, or say nothing: the
tool's order then holds. When question 2 was "a part", write the modules they named into `Run`.

## Done

- `setup.md` has an answer for every question, or the person said the careful value is fine.
- `modules.md` is written, and the person saw it.
- `bruinet scrape factory status` says what is owed.

## Never

- Answer a question for the person, or write `free` on a guess.
- Loosen a guard of `scraper.config.md` because an answer here is loose: the guards are their own decision.
- Start a walk before question 1 has an answer.
