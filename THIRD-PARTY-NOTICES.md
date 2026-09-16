# Third-party notices

The game itself is proprietary (see `LICENSE`). The components listed here are
not ours; they are redistributed under their own terms, which are reproduced in
full in the repository.

## Shippori Mincho B1

- Files: `src/Game/Fonts/ShipporiMinchoB1-{Regular,Medium,SemiBold}.ttf`
- Copyright: the Shippori Mincho Project Authors
- Licence: SIL Open Font License 1.1 — `src/Game/Fonts/OFL-ShipporiMinchoB1.txt`
- Source: https://github.com/google/fonts/tree/main/ofl/shipporiminchob1

The display face: screen names, figures, the acts on a sheet.

## Zen Kaku Gothic New

- Files: `src/Game/Fonts/ZenKakuGothicNew-{Regular,Medium}.ttf`
- Copyright: the Zen Kaku Gothic New Project Authors
- Licence: SIL Open Font License 1.1 — `src/Game/Fonts/OFL-ZenKakuGothicNew.txt`
- Source: https://github.com/google/fonts/tree/main/ofl/zenkakugothicnew

The body face: the lines under a name, notes, the log.

## Both faces are subsetted

The shipped files are not the upstream ones byte for byte. Each was reduced to
the glyphs this game prints — ASCII, the punctuation the screens use and a
handful of CJK marks — which takes the five files from 50 MB to about 120 KB.
Subsetting is a modification, and the OFL allows it: the files keep their
reserved names, are distributed under the same licence, and are never sold on
their own.
