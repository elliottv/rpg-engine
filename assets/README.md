# Fixture assets

Stable, committed assets shared by the sample hosts and the end-to-end tests. They are
generated once and committed so QA and future stories use exactly the same bytes.

## Layout

```
assets/
├── map.tmx               Tiled map (16×12 tiles, 48 px): ground, decor, trees_above + objects
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

## The map (`map.tmx`)

The map is a 16×12 orthogonal world of 48 px tiles (768×576 px). It contains:

| Element | Content |
| --- | --- |
| Map properties | `name` (string `"RPG Engine Fixture Map"`), `author` (string `"RPG Engine QA"`), `is_demo` (bool `true`), `difficulty` (int `3`). |
| `ground` layer | Grass everywhere with a water pond (tiles 2–5) and a sand path (tiles 6–13, rows 6–8). |
| `decor` layer | Trees (tile 3) scattered around the world. |
| `trees_above` layer | A tile layer declaring the Tiled `above_player` boolean property set to `true`; it uses the tree tile to draw a small canopy around the player's starting position. The engine renders `above_player` layers **after** the player (see `docs/Architecture.md`). |
| `objects` layer | An object layer (non-tile) with three objects: a `spawn` point at the player's position (property `facing = "down"`), a `chest` rectangle (properties `locked = true`, `coins = 100`), and a `guard_patrol` polyline (property `speed = 48`). Object layers do not render tiles; they are exposed through `TileMap.ObjectLayers` for game logic (see `docs/api/TileMap.md`). |

The tile layer order is `ground` → `decor` → `trees_above`; object layers are exposed
separately from the tile layers via `TileMap.ObjectLayers`.

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
