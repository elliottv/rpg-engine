# Fixture assets

Stable, committed assets shared by the sample hosts and the end-to-end tests. They are
generated once and committed so QA and future stories use exactly the same bytes.

## Layout

```
assets/
├── map.tmx               Tiled map (16×12 tiles, 48 px, two layers: ground + decor)
├── tiles.tsx             External tileset referenced by map.tmx
├── tiles.png.b64         Base64-encoded 96×96 tileset image (4 tiles: grass, water, tree, sand)
└── characters/
    ├── character_full.png.b64        576×384 full character sheet (8 character slots)
    ├── character_part_body.png.b64    576×384 part sheet (body)
    ├── character_part_face.png.b64    576×384 part sheet (face)
    ├── character_part_hair1.png.b64   576×384 part sheet (hair1)
    ├── character_part_hair2.png.b64   576×384 part sheet (hair2)
    ├── character_part_face_hair.png.b64 576×384 part sheet (face_hair)
    ├── character_part_armour.png.b64  576×384 part sheet (armour)
    └── character_part_head.png.b64    576×384 part sheet (head)
```

## Why are the PNGs stored as `.b64` text?

The PNGs are committed as **base64 text** (`.png.b64`) so they can be version-controlled and
reviewed as text. They decode to the exact PNGs described above:

```bash
base64 -d assets/characters/character_full.png.b64 > character_full.png
```

At run time the hosts and tests decode them (the desktop host materializes them into real
`.png` files at startup; the WebAssembly host decodes the fetched base64 text; the test helper
`FixtureAssets` does the same). The sheet layout is the normative RPG Maker MZ format:
**576×384**, **48×48** cells, **12×8** grid, **8 characters** (4×2), each a 3-frame × 4-direction
block — identical for full and part sheets.

The fixture sheets use the same deterministic per-cell colouring as the engine's own test
helpers (`R = seed * 37 % 256`, `G = column`, `B = row-major cell index`), so pixel-level
tests can predict exact colours. The `map.tmx` scene is a 16×12 world: grass everywhere, a
water pond, a sand path and a few trees.
