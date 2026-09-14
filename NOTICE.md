# Notices

FLAIL! Tools is an independent production by the flail-tools contributors and is not affiliated with
Games Omnivorous. It is published under the Games Omnivorous Third Party Licence.

FLAIL is copyright of Games Omnivorous.

Both sentences above are required by the licence and are reproduced verbatim. They also appear in
the application itself, which is where the licence requires them to be — a notice only in a source
repository is not a notice a player ever sees. The wording is held in
`src/FlailTools.Core/Attribution.cs` and a test asserts that what the interface renders still reads
exactly this once the markup is stripped.

The licence grants no right to translate. If a language is ever added, these two notices must be
rendered in English alongside any translation of them, never instead of it.

## Licence

- **FLAIL!** is by Andre Novoa, published by [Games Omnivorous](https://gamesomnivorous.com).
- The [Games Omnivorous Third Party
  Licence](https://gamesomnivorous.com/pages/flail-license) permits reusing rules and mechanics, and
  forbids copying or translating art or text, using official logos, or implying official status.

## Content

Every table in `src/FlailTools.Web/wwwroot/data/house/` is original to this project. The structures
and procedures follow FLAIL!, which the licence permits; the words filling them do not come from the
book, in whole or in paraphrase.

Each data file carries a `_source` header stating this, and `ProvenanceTests` fails the build if any
file ever names an upstream work.

The dungeon room-stocking checklist FLAIL! uses is credited in the book to the Goblin Punch blog. It
is therefore not Games Omnivorous's to license onward either, which is a second reason the wording
of every stocking outcome here is written from scratch.

## Code

The code in this repository is MIT licensed.

It depends on [Structed.Inkwell](https://github.com/Structed/inkwell), also MIT.
