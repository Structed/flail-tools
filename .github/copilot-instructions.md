# FLAIL! Tools — instructions

FLAIL! Tools is an adventure site generator and a shared dice table for the tabletop RPG **FLAIL!**.
It is a .NET 10 Blazor WebAssembly app with **no server**: it is a static site, generation happens in
the browser, and the dice table connects players' browsers directly to one another over WebRTC.

It is an **unofficial** tool, published under a third-party licence from the game's publisher. That
is not a footnote — it is the constraint that shapes most of the rules below.

## Where the official rules can be found

The rulebook is published free. Read it from the source rather than inferring a table from this
repository's data files:

| What | Where |
| --- | --- |
| **The FLAIL! rulebook, as a PDF** | <https://drive.google.com/file/d/1DzgJUzvWPekIpeZKVZrPhG1r7umsiHf-/view> |
| The game's page, where that PDF is offered and the boxset is sold | <https://gamesomnivorous.com/pages/flail> |
| Games Omnivorous Third-Party Licence | <https://gamesomnivorous.com/pages/flail-license> |

Open the **PDF** when you need to check, correct or extend a generator table: the five generator
tables here are reproduced from it in face order, and the book is the authority on what a face means.
The Drive link is a hosted file and could move; if it does, the game's page is the stable route to
the current one, and `README.md`, `NOTICE.md` and this file should be corrected together.

FLAIL! is by **Andre Novoa**, published by **Games Omnivorous**. The dungeon keying checklist the
book uses is credited in the book to the Goblin Punch blog, and that credit is passed on here rather
than stopping at the publisher.

### What the licence permits and forbids

**Permitted:** rules, mechanics, terminology, and random tables — table entries included.

**Forbidden:** the book's artwork; its written prose (introductions, descriptions, adventure text,
setting text); official logos; implying official status, approval or endorsement; reproducing a
licensed product whole, or building a replacement for one.

So: a table of six flavour words may be reproduced. The paragraph introducing that table may not.
This tool generates the skeleton of a site and leaves every judgement the procedures ask for to a
reader who still needs the book.

### The authorities inside this repository

- **`NOTICE.md`** — the attribution the licence requires, and the record of one obligation that is
  still unmet: the compatibility logo. That asset has to come *from* Games Omnivorous. Do not draw
  one, trace one, or approximate one.
- **`src/FlailTools.Core/Attribution.cs`** — the two mandatory notices, held as code. They are fixed
  wording. Do not edit, shorten, reword or translate them. If a language is ever added, they must
  appear in English *alongside* any translation, never instead of it.
- **The `_source` header in every file under `wwwroot/data/house/`** — per-file provenance: what the
  file is, why it is as it is, whose work its entries come from, and under what terms.

## Layout

```
src/FlailTools.Core/      generation, data, dice, mapping, serialisation — all the logic
  Dice/  Party/           portable: see "The portability rule" below
src/FlailTools.Web/       Blazor WebAssembly, a thin layer over Core
  wwwroot/data/house/     the tables: FLAIL!'s for the five generators, ours for names and maps
  wwwroot/data/ui.json    every word the interface says that is not a table entry
  wwwroot/js/party/       the WebRTC transport, and a vendored Trystero bundle
tests/FlailTools.Core.Tests/
```

Generation logic lives in Core and **never** in a `.razor` file, so it can be tested without a
browser.

The seeded generator engine is [Structed.Inkwell](https://github.com/Structed/inkwell), consumed as
a NuGet package. If something needs to change in the engine, it changes *there* and ships as a new
version rather than being bent around a FLAIL! problem.

## Build, test, run

```
dotnet build FlailTools.slnx
dotnet test FlailTools.slnx
dotnet run --project src/FlailTools.Web
```

`Directory.Build.props` sets `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild`, and CI builds
with `-warnaserror` on **Linux and Windows both** — the golden fixtures are byte-compared, and line
endings differ by platform. A warning is a build failure; fix it rather than suppressing it.

When several worktree sessions are open at once, run the app the way `.github/github-app.yml` does,
so Kestrel takes any free port instead of fighting over the profile's pinned one:

```
dotnet run --project src/FlailTools.Web --launch-profile http -- --urls http://127.0.0.1:0
```

## Rules the build already enforces

These tests exist because each guards something that fails *silently* otherwise. When one goes red,
read what it is protecting before changing anything — the fix is almost never to relax the test.

| Test | What it protects |
| --- | --- |
| `ProvenanceTests` | Only the five generator tables may name FLAIL! as their source; no other file may start to. Every data file needs a complete `_source`, lives under `house/`, and must be one the app actually loads. |
| `PortabilityTests` | `Core/Dice` and `Core/Party` must not reference the rest of Core. See below. |
| `AttributionTests` | The rendered interface, `README.md` and `NOTICE.md` all still carry both notices **verbatim**, once markup is stripped. |
| `GoldenBaselineTests` | Six fixed seeds across every site kind still produce byte-identical output. |
| `FieldPathTests` | The inventory of field paths has not changed. A path seeds its own roll stream *and* keys a lock, so renaming one changes what old seeds produce and orphans every saved lock — with no error at either end. |
| `PageRouteTests` | `/`, `/site`, `/dice` and `/about` are each served by exactly one page. Routes are what people paste into chat windows. |
| `UiWordingTests` | Every literal `Ui.Action("…")` / `Ui.Message("…")` key exists in `ui.json`. A missing key does not throw — it renders the key itself, mid-interface, and survives every build and review. |
| `SiteNameTests` | Name tables stay large, distinct, and joinable into short names. |
| `JsonDefaultsTests` | The `System.Text.Json` source generator discards property initialisers, so every non-nullable property must coerce in its **getter**. Easy to forget on the next property added. |
| `ArtworkTests` | The icons and sharing card are PNGs at their declared sizes, linked relative to the deployment base, with absolute public URLs and the unofficial labelling intact. |

### The portability rule

Nothing in `src/FlailTools.Core/Dice` or `src/FlailTools.Core/Party` may mention
`FlailTools.Core.Data`, `.Generation`, `.Mapping`, `.Model` or `.Serialization` — not even in an
unused `using`. That code is meant to move into the shared engine one day, and the thing that turns
a cheap move into a rewrite is one innocent import added months from now by somebody who just needed
a bit of wording.

`Dice/FlailRolls.cs` is the **single** file allowed to know which game this is, and it must stay
under 130 lines of code. If the presets grow into a rules engine, that is a conversation to have,
not a limit to raise.

## Data tables

This is the highest-risk area in the repository. Two rules matter more than the rest:

**Append. Never reorder, and never delete.** A lock is a stored *position*, not a phrase. Moving a
row rewrites what every previously shared link resolves to, and nothing about it looks like a
mistake.

**Never regenerate a golden fixture to make a test pass.** Regeneration is deliberate:

```
$env:UPDATE_GOLDEN=1; dotnet test FlailTools.slnx   # PowerShell
UPDATE_GOLDEN=1 dotnet test FlailTools.slnx         # bash
```

A changed fixture means every link anybody has already shared now resolves to something else. Read
the diff, and say in the commit message why it moved.

### Face tables must match their die exactly

`Rolls.Face` rolls the die and indexes the row directly — index 0 is a **1** — so a table shorter
than its die would silently yield an empty string rather than an error. Because that failure is
invisible, `GameData` checks the lengths as it loads and throws a `GameDataException` naming the
file, the table and the die:

| File | Table | Length |
| --- | --- | --- |
| `cave.json` | `chambers` | exactly 6 |
| `tower.json` | `floorTypes` | exactly 6 |
| `tower.json` | `floorDetails` | exactly 6 rows of exactly 4, row *n* aligned to `floorTypes` face *n* |
| `tower.json` | `topFloorTypes` | exactly 4 |

An **empty** table is allowed through, as a table not written yet. Any other length is a failure.

`dungeon.json`'s `stocking` is **not** a die table. FLAIL! gives nine keying concepts to choose from
rather than one outcome per face, so the generator picks across all nine; rolling a d6 over it would
leave the last three unreachable.

The axis tables go through `RollContext.Text`, which picks uniformly and does not care how long they
are — but they are kept at the book's die sizes (d6 for dungeons and caves, d10 for towers, d20 for
locations and landmarks) so the implemented procedure matches the one on the page.

Dungeons and caves keep **separate tables and separate field paths** even where an axis name is
shared, so a lock taken on a dungeon cannot resolve silently against a cave.

### The name tables

`site.json` holds 60 stems and 60 tails, shared by all five generators. Each tail deliberately
begins with **one space**, because `NameAssembler.Join` concatenates the parts. Keep at least 40
distinct, non-blank entries per table and every joined name at 32 characters or fewer.

## Code and content conventions

- File-scoped namespaces, nullable enabled, implicit usings, collection expressions,
  `ArgumentNullException.ThrowIfNull` on public entry points, and an explicit `StringComparison` on
  every string comparison.
- XML doc comments carry a `<remarks>` explaining **why** a thing is the way it is, often at length
  and usually with the incident that caused it. That is the house style, not decoration. A rule
  written where it fails loudly beats a rule in a document nobody rereads.
- Tests are named as sentences describing the rule they protect
  (`TheDiceAndPartyCodeKnowsNothingAboutThisGame`), and their failure messages tell the reader what
  to do next.
- No interface wording is hard-coded in a `.razor` file. It goes in `ui.json` and is asked for by
  key.
- Tests reach internals via `InternalsVisibleTo` rather than widening the public API to suit them.

## Files that need special care

- **`wwwroot/js/party/trystero-nostr.js`** — Trystero 0.25.4, MIT, vendored verbatim and pinned to
  LF. Refresh it by the procedure in its banner, keeping the banner. There are no unit tests behind
  that boundary, so open the dice table in two browsers after any bump.
- **`tests/FlailTools.Core.Tests/Fixtures/*.txt`** — pinned to LF in `.gitattributes`. Leave them
  that way.
- **`wwwroot/index.html`** — sharing metadata lives here, in static HTML, so crawlers need not run
  Blazor. It uses absolute URLs under <https://flail-tools.pages.dev/>. There is deliberately **no**
  `og:url` and no canonical link: a crawler should use the shared URL, seed and locks included.
- **`icon.svg` / `open-graph.svg`** — original artwork, MIT, not derived from the game's logo or
  trade dress. Re-export the PNGs at their declared sizes when changing them, review the favicon at
  16 x 16, and keep the **UNOFFICIAL TOOL** label and non-affiliation notice inside the sharing
  image rather than only in the page text around it.

## Git

Commit subjects are short, imperative and sentence-case, with no prefix, tag or ticket number —
"Add a landing page and move the generator to /site", "Read the dice as a poker hand".

Work on a feature branch and open a pull request. **Never commit or push to `main`.**

## Do not

- Reorder or delete a table row, or rename a field path.
- Run `UPDATE_GOLDEN=1` to turn a red test green without reading the diff.
- Reword, shorten or translate the two licence notices.
- Copy the book's prose, descriptions, adventure text or artwork into this repository.
- Add a Games Omnivorous logo from anywhere but Games Omnivorous.
- Give `Core/Dice` or `Core/Party` a dependency on the rest of Core.
- Put generation logic in a `.razor` file, or an English sentence anywhere but `ui.json`.
