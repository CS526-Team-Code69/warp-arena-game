# UI font

`NotoSansCJKsc-Regular.otf` is the static Simplified Chinese regular weight of Noto Sans CJK, taken unmodified from the official notofonts/noto-cjk repository. It is 16,437,364 bytes, SHA-256 `2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b`.

## Licence

The font is distributed under the SIL Open Font License 1.1. The original copyright notice and the full licence text are in `OFL-NotoSansCJK.txt`, which must stay next to the font. Source URLs, byte sizes and SHA-256 checksums for both files are recorded in the JSON file in this folder.

## How the game uses it

- Both the English and the Chinese locale use this one packaged Font asset; English text uses its Latin glyphs.
- It is referenced as a project asset. The game does not look up a font installed on the player's operating system.
- The full font is kept, not a subset, because the UI text is still changing. A smaller subset for the WebGL build is possible later; its size saving has not been measured.

## Sources

- Font: https://github.com/notofonts/noto-cjk/blob/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf
- Licence: https://github.com/notofonts/noto-cjk/blob/main/Sans/LICENSE
