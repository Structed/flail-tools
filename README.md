# FLAIL! Tools

An unofficial adventure site generator for **FLAIL!**, in your browser. It rolls dungeons, caves,
wizard towers and hexcrawl locations, draws each one a hand-inked map, and gives you a short link
that rebuilds exactly what you saw.

Free, no account, no tracking, no server — it is a static site and everything happens on your
machine.

> FLAIL! Tools is an independent production by the flail-tools contributors and is not affiliated with Games Omnivorous. It is published under the Games Omnivorous Third Party Licence.
>
> FLAIL is copyright of Games Omnivorous.

The licence is published [here](https://gamesomnivorous.com/pages/flail-license).

## What it generates

FLAIL! calls them Adventure Sites, and there are five sorts:

| Kind | Rolled on | Axes |
| --- | --- | --- |
| Dungeon | d6 | Flavour · Type · Location · Key Feature · Creatures |
| Cave | d6 | Flavour · Type · Location · Key Feature · Creatures |
| Wizard tower | d10 | Shape · Occupant · Reaction · Goal |
| Location | d20 | Location · Biome · Condition · Key Feature · Occupant |
| Landmark | d20 | Landmark · Biome · Condition · Key Feature · Occupant |

Dungeons are stocked room by room on a d6 and keyed from the entrance to the final area. Towers are
a stack of d6s with a d4 balanced on top, each die a floor. Caves are a handful of dice dropped on
a page: the one nearest the edge is the way in, the one nearest the middle is the heart of it, and
anything that bounces off the paper has to be reached some other way.

## Using it

Every field has a lock and a re-roll. Lock the ones you like and press **Roll a site** again: the
locked fields stay put and everything else changes around them. Re-rolling a single field works on
its own stream, so it cannot disturb anything else on the page.

The address bar always describes what is on screen — seed, kind, locks and all — so copying the
link is the whole of sharing. **Download** writes the same thing as a file, for when a link is not
enough.

## On content, and why the tables are ours

FLAIL! has no SRD and is not openly licensed. The Games Omnivorous Third Party Licence permits
reusing **rules and mechanics** and forbids **copying or translating art or text**.

So this tool implements the book's procedures — which is allowed, and is the interesting part
anyway — and fills them with tables written from scratch. **Nothing here is transcribed or
paraphrased from FLAIL!**, and every data file records that claim in its own `_source` header, with
a test that fails if any file ever declares an upstream work.

This is a tool for people who already own the game. If you do not, [buy
it](https://gamesomnivorous.com) — it is very good, and none of this works without it.

## Running it

```
dotnet build FlailTools.slnx
dotnet test FlailTools.slnx
dotnet run --project src/FlailTools.Web
```

### Golden baselines

A handful of fixed seeds are generated and compared against committed fixtures in
`tests/FlailTools.Core.Tests/Fixtures/`. They exist because a shared link is only worth sharing if
it rebuilds the same thing everywhere — the sibling Mausritter generator once emitted CRLF on
Windows and LF on Linux, so the same seed produced different bytes depending on which machine built
it.

To regenerate them deliberately:

```
UPDATE_GOLDEN=1 dotnet test FlailTools.slnx        # bash
$env:UPDATE_GOLDEN=1; dotnet test FlailTools.slnx  # PowerShell
```

A changed fixture means every link anybody has already shared now resolves to something else. Read
the diff.

CI runs the suite on Linux *and* Windows for the same reason.

### Icon artwork

`src/FlailTools.Web/wwwroot/icon.svg` is the editable, original folded-map drawing. It is not the
game's logo; see [NOTICE.md](NOTICE.md#icon-artwork) for provenance. Export the full square canvas
with its opaque paper background to PNG when changing it:

| File (in `wwwroot`) | Size | Use |
| --- | --- | --- |
| `favicon.png` | 32 x 32 | Browser tabs and bookmarks |
| `icon-192.png` | 192 x 192 | High-resolution browser icon |
| `apple-touch-icon.png` | 180 x 180 | Apple home screens |

Review the favicon at **16 x 16** as well as native size. Keep the broad ink strokes and simple
folds readable without relying on colour, lettering or details from the game's artwork.

### Sharing card

`src/FlailTools.Web/wwwroot/open-graph.png` is the **1200 x 630** Open Graph and Twitter large-image
card. Its editable source is `open-graph.svg`, which references `icon.svg`; keep both SVG files
together when editing or exporting. Export the full canvas with its opaque paper background and
review at a reduced sharing-preview size. Keep the prominent **UNOFFICIAL TOOL** label and
non-affiliation notice in the image, not just in surrounding page text.

Sharing metadata lives in the static `wwwroot/index.html` so crawlers do not need to run Blazor.
It uses absolute URLs under **https://flail-tools.pages.dev/**. If the public host changes, update
those URLs, the address printed in the card, and the expectations in `ArtworkTests`. Every shared
site uses this generic card; it does not depict the particular seed encoded in a link. There is
deliberately no fixed `og:url` or canonical link: a crawler should use the shared URL, including
its seed, locks and re-roll counters, rather than treating every generated site as the home page.

## Layout

```
src/FlailTools.Core/      generation, data, mapping, serialisation — all the logic
src/FlailTools.Web/       Blazor WebAssembly, a thin layer over Core
  wwwroot/data/house/     the tables, all original to this project
  wwwroot/data/ui.json    every word the interface says that is not a table entry
tests/FlailTools.Core.Tests/
```

Generation logic lives in Core and never in a `.razor` file, so it can be tested without a browser.

Built on [Structed.Inkwell](https://github.com/Structed/inkwell), a game-agnostic seeded generator
engine. If something needs to change in the engine, it changes there and ships as a new version
rather than being bent around a FLAIL! problem.

## Licence

The code is MIT. See [NOTICE.md](NOTICE.md) for the attribution the Games Omnivorous Third Party
Licence requires.
