# Packaged Chinese and English UI font

`NotoSansCJKsc-Regular.otf` is the unmodified static Simplified Chinese regular font from the official notofonts/noto-cjk repository. It is 16,437,364 bytes. The original copyright notice and SIL Open Font License 1.1 are included in `OFL-NotoSansCJK.txt`; source URLs and SHA-256 are in `source-receipt.json`.

Both Chinese-team and English-showcase locales use this packaged Font asset. English uses its Latin glyphs. It is an asset reference, not a lookup for a font installed on the player's operating system.

This project keeps the full font, unmodified. The published web builds ship a subset with only the characters the game uses, cut with fontTools in a separate copy of the project at build time, so this file never changes.

Official sources:

- Font: https://github.com/notofonts/noto-cjk/blob/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf
- License: https://github.com/notofonts/noto-cjk/blob/main/Sans/LICENSE
