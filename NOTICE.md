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
- It has no SRD and is not openly licensed. Reuse is governed by the [Games Omnivorous Third Party
  Licence](https://gamesomnivorous.com/pages/flail-license), quoted below rather than summarised.
  This file used to summarise it, and the summary understated what the licence grants.

### What the licence says

The licence covers FLAIL! and its compatible settings — Undying Sands, Bottled Sea and Boreal
Frostlands — and allows free or commercial material based upon or declaring compatibility with them
without express permission from Andre Novoa or Games Omnivorous. Quoted from the licence:

> **Without explicit permission, you may not:**
>
> - Copy or translate the art or text on any of the above products.
> - Use any official logo (Games Omnivorous, FLAIL, Undying Sands, Bottled Sea, Boreal Frostlands
>   logos).
> - State or imply that your work is an official Games Omnivorous creation.
>
> **You may:**
>
> - Use, copy and modify any templates present in the above products.
> - Use, reference and modify any of the rules and mechanics.
> - Reference any locations, creatures, characters or factions mentioned in the above products.

It also requires the two notices at the top of this file, and adds that "Games Omnivorous takes no
responsibility for any legal claims against your product."

### On "templates"

The licence permits copying templates and forbids copying text, and a roll table is plausibly both.
This project reads "templates" as the frame, not the filling.

A table's skeleton — which die, which axes, how many rows, how the results combine — is a template,
and is equally a rule or mechanic, so it is permitted twice over. That is the part this tool
implements. The entries inside a table are authored prose, and prose is what "copy or translate the
art or text" forbids. Reading "templates" widely enough to swallow table entries would leave the
text prohibition with almost nothing left to bite on, because in a rules-light book most of the text
*is* tables. Both clauses are read here so that each has effect.

That is a reading, not a ruling, and not legal advice; only Games Omnivorous can settle it. It is
not the only reason the tables here are original, either — see [Content](#content) for a constraint
that binds whichever way the question falls.

## Content

Every table in `src/FlailTools.Web/wwwroot/data/house/` is original to this project. The structures
and procedures follow FLAIL!, which the licence permits; the words filling them do not come from the
book, in whole or in paraphrase.

Each data file carries a `_source` header stating this, and `ProvenanceTests` fails the build if any
file ever names an upstream work.

The dungeon room-stocking checklist FLAIL! uses is credited in the book to the Goblin Punch blog. It
is therefore not Games Omnivorous's to license onward either — which binds however the templates
question above is read, and is a second reason the wording of every stocking outcome here is written
from scratch.

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
