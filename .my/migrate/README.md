# Bruinet Migrate

Migrate is the module of Bruinet that converts a product to another architecture a unit at a time, and keeps the
evidence that each unit still does what it did. It was called Gaminet. It has **no code yet**: nothing is built
ahead of a real migration. Then it gets `Bruinet.Migrate` and a `bruinet migrate` command.

## What it will hold

| Part | Holds |
| --- | --- |
| The inventory and the ledger | The old system's units, and per unit what was converted, by which rule, with which evidence (parity per layer) |
| The transform rules and packs | How a unit of the source architecture becomes one of the target |
| The formats | Inventory, architecture description, transform rule, ledger ([formats.md](formats.md)) |

A migration project keeps its record in `.migrator/` at its own root, not here.

## What it uses

Spec for the behaviour (a unit's spec says what must still hold, captured once), Test for the proof (comparing old
and new runs), and Develop's loop for the conversion. Inside this repository Migrate may reference the modules below
it; nothing references Migrate (`ModuleTests`).
