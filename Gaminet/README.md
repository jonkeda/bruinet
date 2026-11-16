# Gaminet

Gaminet is the migrator: it converts a product to another architecture a unit at a time, and
keeps the evidence that each unit still does what it did. It lives in Bruinet's repository for
now and may become its own later.

It is not built yet. This folder is the layout and an empty tool, so the first step of a
migration has a place to land. Nothing is built ahead of a case that needs it.

## The four parts

| Part | Does |
| --- | --- |
| **Bruinet** | Tests: runs the scenarios and the specs' examples |
| **Lirenet** ([../Lirenet](../Lirenet/README.md)) | Specs: the format, `lirenet check`, `lirenet trace` |
| **Gaminet** | Migration: the inventory, the transform rules, the ledger |
| **Frainet** ([../Frainet](../Frainet/README.md)) | The factory: the loops that use the other three |

## What it will hold

| Folder | Holds |
| --- | --- |
| `src/Gaminet.Cli` | The `gaminet` command. Today: `--help` |
| `src/Gaminet.Ledger` | The inventory of the old system's units and the ledger of what was converted, by which rule, with which evidence. Not there yet |
| `src/Gaminet.Rules` | The transform rules and packs. Not there yet |
| `docs/` | The formats: inventory, architecture description, transform rule, ledger. Not written yet |

A migration project keeps its record in `.migrator/` at its own root, not here.

## Commands

From this folder:

```bash
dotnet build Gaminet.slnx
```

```bash
dotnet run --project src/Gaminet.Cli -- --help
```

## What it depends on

Lirenet and Bruinet, as commands and files only: the spec of a unit says what must still hold,
and its tests prove it. No project reference crosses this folder, so it can move to a repository
of its own as a copy.
