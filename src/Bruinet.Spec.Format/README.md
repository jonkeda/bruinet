# Bruinet.Spec.Format

Reads a spec file (`*.spec.md`) into a model: the title and its journey id, the header, the sections, the rules table,
and the examples and chapters under `## Examples`, each with the line it is on. It checks nothing and depends on
nothing: `Bruinet.Spec` checks a spec against the format, and `Bruinet.Bdd` runs a spec's examples as scenarios, both
from this one reading. The format is described in [docs/spec/spec-format.md](../../docs/spec/spec-format.md).

```csharp
var spec = SpecReader.Read(File.ReadAllText("TOD.02.spec.md"));
foreach (var example in spec.Examples)
    Console.WriteLine($"{spec.JourneyId}.R{example.RuleNumber} {example.Name} (line {example.Line})");
```
