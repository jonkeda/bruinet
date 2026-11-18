# AD-044: One Product, Four Modules

**Decided:** Bruinet is one product in four modules: **Test** (the drivers, page objects, BDD engine, Scraper and the
rest of what Bruinet was), **Spec** (the spec format, its check and its trace), **Develop** (the loops that do the
work) and **Migrate** (converting a product to another architecture). One repository, one solution (`Bruinet.sln`),
one build (`Quality.props`), one version and release, one command line (`bruinet spec …` beside `bruinet scrape …`).
A module is known by its projects' names: `Bruinet.Spec*`, `Bruinet.Develop*`, `Bruinet.Migrate*`; every other
project is Test. References point one way: Spec references only Spec; Test reaches Spec only through
`Bruinet.Spec.Format`, the format's one reader; nothing below references Develop or Migrate; Develop does not
reference Migrate. Something two modules both need lives in the lower of them, as a library with no opinions
(the spec reader is Spec's, and Test uses it). Develop judges nothing itself: it calls the other modules' checks
and records their verdict. Develop and Migrate get code only when a case needs it.

**Why:** the parts depend on each other and must not copy code. Four products would have been one product with four
release trains: a version matrix and a pull request per repository for every change across them, for three parts
with no users. A separate folder per part had already made two readers of the spec format and a command that started
another as a process. Packages, not products, serve the different user groups: `Bruinet.Spec` needs no driver.
Evidence: [one-or-four.md](https://github.com/exact-mobile-apps/test-tooling/blob/b99faf20/.my/structure/one-or-four.md),
[plan.md](https://github.com/exact-mobile-apps/test-tooling/blob/b99faf20/.my/structure/plan.md).

**Ruled out:**
- a part with its own repository, solution, build settings or release, until a user group uses it alone and the
  shared version hurts them, its release pace really differs, or it needs other access or another licence;
- a second reader of the spec format, or a module reaching another by starting it as a process;
- a reference from Test into Spec other than `Bruinet.Spec.Format`, and any reference into Develop or Migrate from
  below;
- code in Develop or Migrate ahead of a case that needs it.

**Broken when:** `ModuleTests.ReferencesBetweenModules_PointOneWay` fails; `SolutionTests` finds a project outside
`Bruinet.sln`; a spec is read by anything but `SpecReader`; a folder at the root holds its own `global.json` or
solution.
