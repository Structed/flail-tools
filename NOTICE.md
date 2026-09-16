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

Every table in `src/FlailTools.Web/wwwroot/data/house/` is original to this project. The licence now
permits reusing FLAIL!'s own random tables and their entries; these were written from scratch anyway,
and the words filling them do not come from the book, in whole or in paraphrase.

Each data file carries a `_source` header stating this, and `ProvenanceTests` fails the build if any
file ever names an upstream work. That test is stricter than the licence now requires. It stays
because it is what keeps the `_source` headers honest: if book tables are ever used, the headers have
to say so, and loosening the test should be a deliberate, reviewed act rather than a quiet one.

The dungeon room-stocking checklist FLAIL! uses is credited in the book to the Goblin Punch blog. It
is therefore not Games Omnivorous's to license onward either, which is a second reason the wording
of every stocking outcome here is written from scratch.

## Icon artwork

The folded-map icon in `src/FlailTools.Web/wwwroot/icon.svg` is original artwork by the
flail-tools contributors, released under the MIT licence. The favicon, 192px icon and Apple touch
icon are PNG exports of that drawing. It uses the site's ink-and-paper palette and is not copied,
traced or adapted from the FLAIL! logo, book artwork or trade dress. It identifies this unofficial
tool, not the game or its publisher.

`src/FlailTools.Web/wwwroot/open-graph.svg` is an original sharing-card layout under the same MIT
licence, reusing that folded-map drawing. `open-graph.png` is its raster export. Both the card and
its sharing metadata identify the tool as unofficial; the card also carries a non-affiliation
notice. Its title is ordinary typeset text, not the FLAIL! logo.

## Code

The code in this repository is MIT licensed.

It depends on [Structed.Inkwell](https://github.com/Structed/inkwell), also MIT.
