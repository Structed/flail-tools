# Notices

FLAIL! Tools is an independent production by the flail-tools contributors and is not affiliated with
Games Omnivorous. It is published under the Games Omnivorous Third-Party Licence.

FLAIL is copyright of Games Omnivorous.

Both sentences above are required by the licence and are reproduced verbatim. They also appear in
the application itself, which is where the licence requires them to be — a notice only in a source
repository is not a notice a player ever sees. The wording is held in
`src/FlailTools.Core/Attribution.cs` and a test asserts that what the interface renders still reads
exactly this once the markup is stripped.

The licence gives these two notices as fixed wording and grants no right to restate them in another
language, even though it now permits translating rules text and table entries. If a language is ever
added, the notices must be rendered in English alongside any translation of them, never instead of
it.

## Licence

- **FLAIL!** is by Andre Novoa, published by [Games Omnivorous](https://gamesomnivorous.com).
- The [Games Omnivorous Third-Party
  Licence](https://gamesomnivorous.com/pages/flail-license) permits reusing rules, mechanics,
  terminology and random tables, table entries included. It forbids reproducing artwork or written
  prose, using official logos, implying official status, and reproducing a licensed product whole or
  building a replacement for one.

## Outstanding obligation

The licence **requires** the relevant Games Omnivorous compatibility logo to identify a work as
compatible with FLAIL!. This tool does not carry one yet, so that requirement is currently unmet.

The asset has to come from Games Omnivorous — it is one of the logos the licence otherwise forbids
reproducing, and it may not be altered or used to suggest the tool is official, approved or
endorsed. It cannot be drawn from scratch here; the published file has to be obtained and added.

## Content

The five generator tables in `src/FlailTools.Web/wwwroot/data/house/` are FLAIL!'s own, reproduced
in face order: the Dungeons, Caves and Wizard Towers themes from the Adventure Sites chapter, and
the d20 Locations and Landmarks tables from the hexcrawl chapter. Section 1 of the licence permits
this expressly — it covers rules text, random tables, table entries and terminology.

Games Omnivorous publish the rulebook free at <https://gamesomnivorous.com/pages/flail>, which is
where these were taken from.

No prose came with them. The book's introductions, descriptions, adventure text and setting text are
not reproduced here, and neither is any artwork; section 2 still forbids both. Nor is this a
replacement for the book — it generates the skeleton of a site and leaves every judgement the
procedures ask for to the reader, who needs the rules to make anything of it.

The site names and the map silhouettes are ours outright, because FLAIL! has no equivalent tables.

Each data file carries a `_source` header naming the work its entries come from, or declaring that
they were written for this tool. `ProvenanceTests` fails the build if a generator table stops naming
FLAIL!, or if any other file starts to — so the split is a reviewed decision rather than a drift.

The dungeon keying checklist FLAIL! uses is credited in the book to the Goblin Punch blog. That
credit is passed on here rather than stopping at Games Omnivorous.

## Icon artwork

The folded-map icon in `src/FlailTools.Web/wwwroot/icon.svg` is original artwork by the
flail-tools contributors, released under the MIT licence. The favicon, 192px icon and Apple touch
icon are PNG exports of that drawing. It uses the site's ink-and-paper palette and is not copied,
traced or adapted from the FLAIL! logo, book artwork or trade dress. It identifies this unofficial
tool, not the game or its publisher.

`src/FlailTools.Web/wwwroot/open-graph.svg` and `open-graph-site.svg` are original sharing-card
layouts under the same MIT licence, reusing that folded-map drawing. `open-graph-dice.svg` is the
same layout with its own original drawing of a twenty-sided die and a six-sided die: broad ink
outlines in the site's palette, not copied, traced or adapted from FLAIL!'s dice, artwork or trade
dress. The `.png` files beside them are their raster exports. Every card and the sharing metadata
around it identify the tool as unofficial; each card also carries a non-affiliation notice. Their
titles are ordinary typeset text, not the FLAIL! logo.

## Code

The code in this repository is MIT licensed.

It depends on [Structed.Inkwell](https://github.com/Structed/inkwell), also MIT.

### Trystero

The dice table's peer-to-peer connection uses
[Trystero](https://github.com/dmotz/trystero) 0.25.4 by Dan Motzenbecker, MIT licensed. The Nostr
strategy bundle is vendored verbatim at
`src/FlailTools.Web/wwwroot/js/party/trystero-nostr.js`, with its origin and refresh instructions in
a banner at the top of the file.

Vendoring rather than loading it from a CDN keeps the site working when a CDN does not, and means no
third party can change what runs on the page between one session and the next. The bundle carries
its own copy of [@noble/secp256k1](https://github.com/paulmillr/noble-secp256k1) by Paul Miller,
also MIT licensed, which Trystero uses to sign the Nostr events that carry the signalling.

Neither library sees a roll. They establish the connection; the dice travel directly between
browsers over it.
