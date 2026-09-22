# FLAIL! Tools

An unofficial adventure site generator for **FLAIL!**, in your browser. It rolls dungeons, caves,
wizard towers and hexcrawl locations, draws each one a hand-inked map, gives you a short link that
rebuilds exactly what you saw, and runs a shared dice table for the whole party.

Free, no account, no tracking, no server — it is a static site and everything happens on your
machine.

> FLAIL! Tools is an independent production by the flail-tools contributors and is not affiliated with Games Omnivorous. It is published under the Games Omnivorous Third-Party Licence.
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

The front page lists the tools and does nothing else. The generator is at `/site` and the dice
table at `/dice`; nothing is the default, and the wordmark goes back to the list. Links shared
before the generator moved off the root address now open the front page rather than the site they
described — repaste them from `/site` if you still want them.

Every field has a lock and a re-roll. Lock the ones you like and press **Roll a site** again: the
locked fields stay put and everything else changes around them. Re-rolling a single field works on
its own stream, so it cannot disturb anything else on the page.

The address bar always describes what is on screen — seed, kind, locks and all — so copying the
link is the whole of sharing. **Download** writes the same thing as a file, for when a link is not
enough.

## Rolling dice together

`/dice` is a dice table the whole party shares. One player presses **Start a table** and sends
round the link; everyone who opens it sees every roll appear on their own screen as it happens.
There are buttons for the two rolls FLAIL! asks for constantly — a To Hit pool of d6s counting 1s,
and a save rolled at or under an attribute — and a box for anything else in ordinary notation:
`d20`, `2d6+1`, `4d6kh3`.

**Who is here** lists everybody at the table by name, yours first. Each browser says what it is
called when somebody new arrives, so a player who has joined and not yet rolled is visible rather
than an increment on a headcount, and changing your name mid-session updates it on every screen. A
peer who is connected but has not introduced themselves yet — usually a moment's handshake — shows
as *still saying hello* rather than quietly going missing from a list the count above it disagrees
with.

Both presets take an **advantage or disadvantage**, in the steps the rules use: a To Hit gains or
loses a die, a save rolls a second d20 and keeps the kinder result, and stacking stops at three
dice. Pressing the step you are already on puts the roll back to straight.

Tick **Roll privately** and the table is told you rolled and nothing else. No dice, no total, no
reading. That entry is a ghost when it leaves the machine, so it is not a matter of the other
players' browsers politely declining to look: what they receive has nothing in it to look at. Your
own screen still shows the dice, marked as private, because it is a secret from the table and not
from you.

### Talents, and why there are none

A To Hit roll also reports its **poker results** — pair, two pairs, triplet, poker, full house,
sequence — beside the hit itself. That is not decoration. It is the mechanic almost every talent,
legendary weapon and creature ability in FLAIL! is keyed to: a Cutthroat who rolls two pairs on a To
Hit attacks again immediately, and until now they had to squint at five dice to notice.

What the tool deliberately does not do is know that. Encoding the talents would make this a rules
engine, would need extending for every new class and monster, and would still be wrong for the table
that house-ruled one of them. Worse, the interesting cases are the ambiguous ones — some talents fire
on a To Hit roll and some only on a successful one — and a tool that quietly picked a side would be
making rulings nobody asked it to make. So the dice are read honestly and completely, and the player
reads their own sheet, which is the part they came for.

Those results are never sent. Each screen works them out again from the faces it received, which
have already been checked, so a peer cannot announce a full house they did not roll.

### How it works without a server

There is no server, because there is nowhere to put one — this is a static site on GitHub Pages.
Browsers connect **directly to each other** over WebRTC, using
[Trystero](https://github.com/dmotz/trystero) over public [Nostr](https://nostr.com) relays to find
one another in the first place.

The relays only carry the introduction. They see an opaque room identifier derived from the table
code and encrypted handshake traffic; they never see a roll, a name, or a result, and they store
nothing. Once two browsers have found each other the relays are out of the conversation and the
dice go straight between the two, encrypted end to end. Close the tab and the table is gone — there
is nothing anywhere to delete.

Worth knowing before you rely on it:

- **Some networks will not allow it.** Peer-to-peer needs a route between the two machines, and a
  strict corporate firewall or an unlucky pair of mobile carriers can refuse to give one. There is
  no fallback relay to hide behind, so when it fails it fails visibly rather than quietly.
- **The table code is the whole of the security.** Twelve random characters, 60 bits: nobody is
  guessing one, but anybody you send it to can listen. Treat the link like a key.
- **Most public relays are down at any given moment.** That is normal and the page copes; it only
  needs one, and it says out loud when it has none.
- **Nobody is the host.** The history a late arrival receives comes from whoever is already there,
  which means a roll made before anybody was connected is a roll nobody else will ever see.
- **A name is a claim, not an identity.** There are no accounts here, so the table shows what each
  browser says it is called. Two players may pick the same name, and the person who gave you the
  code could pick yours. The connection a message arrived on is what keeps the seats apart.

The dice themselves are C# in `FlailTools.Core/Dice`, and the table in `FlailTools.Core/Party`;
neither knows what a browser is, which is what makes them testable without one. The channel carries
two kinds of message and no others — a roll, and a hail saying what a player is called — each with
its own strict reader, because a channel that can only carry dice and names cannot be talked into
carrying anything else. `party.js` opens the room and passes those as opaque strings. It has no
vocabulary of its own — no dice, no FLAIL!, no wording — so the transport could be replaced without
touching a rule.

`PortabilityTests` enforces that split: nothing in those two folders may reference the generator,
the data loader or the site's model, and `FlailRolls.cs` is the single file allowed to know what
game this is. That is the groundwork for lifting the dice and the table into
[Structed.Inkwell](https://github.com/Structed/inkwell) once they have proven themselves here, so a
sibling tool can share them.

## On content, and where the tables come from

The Games Omnivorous Third-Party Licence permits reusing **rules, mechanics, terminology and random
tables**, table entries included. It still forbids reproducing the **artwork** and the **written
prose** — introductions, descriptions, adventure text, setting text — using official logos, implying
official status, or reproducing a licensed product whole and building a replacement for one.

The five generator tables are FLAIL!'s own, reproduced in face order: the Dungeons, Caves and Wizard
Towers themes from the Adventure Sites chapter, and the d20 Locations and Landmarks tables from the
hexcrawl chapter. Each of those files names FLAIL! in its `_source` header, and `ProvenanceTests`
fails the build if one of them stops naming it — or if any other file starts to. Games Omnivorous
[publish the rulebook free](https://gamesomnivorous.com/pages/flail), as a
[PDF](https://drive.google.com/file/d/1DzgJUzvWPekIpeZKVZrPhG1r7umsiHf-/view), which is what these
were read off.

Two things are still ours, because the book has no equivalent: the site names, and the silhouettes
the maps are drawn from.

The shared name tables in `src/FlailTools.Web/wwwroot/data/house/site.json` contain 60 original stems
and 60 tails, making 3,600 short, kind-neutral names for all five generators. `NameAssembler.Join`
concatenates the parts, so each tail deliberately starts with one space. Keep at least 40 distinct,
nonblank entries in each table and every joined name within 32 characters. Do not reorder existing
entries: locks store their positions. Changes to these tables also require deliberate
[golden-baseline regeneration and diff review](#golden-baselines).

The five per-kind files hold 349 further entries, sized to the die each axis is read with:

| File | Axes | Face tables |
| --- | --- | --- |
| `dungeon.json` | five @ d6 | `stocking` is **not** a die table — see below |
| `cave.json` | five @ d6 | `chambers` @ **exactly 6** |
| `tower.json` | four @ d10 | `floorTypes` @ **exactly 6**, `floorDetails` @ **6 rows of exactly 4**, `topFloorTypes` @ **exactly 4** |
| `location.json` | five @ d20 | — |
| `landmark.json` | five @ d20 | — |

A tower floor is two rolls, as it is in the book: the d6 gives the kind of room and a d4 gives which
one, so `floorDetails` row *n* belongs to `floorTypes` face *n* and the two must stay aligned.
`dungeon.json`'s `stocking` is the odd one out — FLAIL! gives nine keying concepts to choose from
rather than a table with one outcome per face, so the generator picks across all nine. Rolling a d6
over it would leave the last three unreachable.

The face tables are read by `Rolls.Face`, which rolls the die and indexes the row directly, so a
table shorter than its die silently yields an empty string rather than an error. They must stay
exactly as long as the die, in face order: index 0 is a 1. The axis tables go through
`RollContext.Text`, which picks uniformly and does not care how long they are — but they are kept at
the book's die sizes so the implemented procedure matches the one on the page. Dungeons and caves
deliberately keep separate tables and separate field paths even where an axis name is shared. As
with the name tables, append rather than reorder: a lock is a position, not a phrase.

This is a tool for people who are playing the game. Games Omnivorous [publish the rulebook
free](https://gamesomnivorous.com/pages/flail) as a
[PDF](https://drive.google.com/file/d/1DzgJUzvWPekIpeZKVZrPhG1r7umsiHf-/view), and sell it as a
physical boxset — buy it, it is very good, and none of this works without it.

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

### The vendored Trystero bundle

`src/FlailTools.Web/wwwroot/js/party/trystero-nostr.js` is
[Trystero](https://github.com/dmotz/trystero) 0.25.4, MIT licensed, committed verbatim rather than
fetched from a CDN so the dice table keeps working when a CDN does not and nothing third-party can
change under the page between sessions. It is marked `linguist-vendored` and pinned to LF.

To refresh it, download
`https://esm.sh/trystero@<version>/es2022/trystero.bundle.mjs`, replace everything below the banner
comment, keep the banner, and check the bundle still declares no imports of its own. It is saved as
`.js` and not `.mjs` because GitHub Pages serves `.mjs` with a MIME type browsers refuse to import.

Trystero's action API is settable properties rather than callbacks — `action.onMessage = handler`,
`action.send(payload, { target })`, `room.onPeerJoin = handler` — and handlers are called with
`(payload, { peerId })`. A version that changes this will break `party.js` loudly rather than
quietly, but there are no unit tests behind that boundary, so the dice table wants opening in two
browsers after any bump.

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
  wwwroot/data/house/     the tables: FLAIL!'s for the five generators, ours for names and maps
  wwwroot/data/ui.json    every word the interface says that is not a table entry
tests/FlailTools.Core.Tests/
```

Generation logic lives in Core and never in a `.razor` file, so it can be tested without a browser.

Built on [Structed.Inkwell](https://github.com/Structed/inkwell), a game-agnostic seeded generator
engine. If something needs to change in the engine, it changes there and ships as a new version
rather than being bent around a FLAIL! problem.

## Licence

The code is MIT. See [NOTICE.md](NOTICE.md) for the attribution the Games Omnivorous Third-Party
Licence requires.
