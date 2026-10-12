# Data catalogs: tech tree, shop, worlds

Three tables drive most of the game's tuning. Each one still lives in code as the default, and each one can be overridden by a ScriptableObject database that designers edit in the Inspector.

| Catalog (code default) | Database type | Asset path (optional) | What it holds |
|---|---|---|---|
| `Systems/TechCatalog.cs` | `TechDatabase` | `Assets/Resources/Data/TechDatabase.asset` | Research tree: name, text, science cost, prerequisites, CRED cost on completion, launch unlock, research-rate bonus |
| `Systems/ShopCatalog.cs` | `ShopDatabase` | `Assets/Resources/Data/ShopDatabase.asset` | Hero shop items: vendor, kind, class lock, price, bonuses |
| `Systems/World/CelestialBodyCatalog.cs` | `CelestialBodyDatabase` | `Assets/Resources/Data/CelestialBodyDatabase.asset` | Full `CelestialBodyProfile` per world: logs, palette, sky, layout counts, campaign gates, stockpile, fauna |

The database types are in `Assets/Scripts/Systems/Databases/`.

## How the override works

- **No asset means no change.** If there is no asset at the path above, the catalog returns its built-in table: the same objects, in the same order. A fresh checkout ships with no assets, so the game plays exactly as it did before the databases existed.
- **An asset overrides entry by entry.** Its entries come first, in the order the asset lists them. Any id the asset leaves out keeps its built-in entry, added after the asset's entries. A half-finished asset can't make a tech, item or world disappear from under a save.
- **Blank text falls back.** An empty name, description or log line uses the built-in text for that id.
- **Bad rows are skipped.** A row with id `None` is ignored. If an id appears twice, only the first row is used. Both log a warning.
- **World rules.** The set of worlds and the campaign order stay in code (`CelestialBodyId`, `CelestialBodyCatalog.All`). The asset can retune a world but can't add one. A `ResourceWeights` table that isn't exactly four long falls back to the built-in one. The catalog hands out a copy of the asset's profile, so play mode never writes back into the asset.
- **Loaded once.** Each catalog loads its asset on first use and caches the result for the session. `TechCatalog.ReloadDatabase()` (and the same method on the other two) forgets the cache, and the export menu calls it for you.
- **Same API.** Call sites still use `TechCatalog.All` / `Get`, `ShopCatalog.Get` and the buying rules, and `CelestialBodyCatalog.Get` / `Mars()` and the rest. Use `Defaults` / `Default(id)` when you need the built-in table even if an asset exists.

## Creating the assets

In the editor, run **Solar Majesty > Data > Export catalogs to assets**. It writes all three databases from the built-in tables into `Assets/Resources/Data/`. If any of them already exist, it asks before replacing them, because replacing throws away any edits made in those assets.

Headless: `Unity -batchmode -projectPath . -executeMethod SolarMajesty.EditorTools.CatalogDatabaseExporter.ExportCli -quit`. It exits with 0 when all three assets were written.

You can also make an empty database with **Assets > Create > Solar Majesty > Data > …** and add only the rows you want to change. Every id you leave out keeps its default.

To go back to the code tables, delete the asset.

## Save compatibility

Saves store techs, shop items and worlds by their **enum number** (`TechId`, `ShopItemId`, `CelestialBodyId`), never by name or list position. That means:

- Reordering rows, renaming display names and retuning numbers in an asset are all save-safe.
- **Never renumber or delete an enum value.** If you add a new tech or item, add the enum value in code first, then give it a row in the code table (the default) and, if you like, in the asset.
- An asset row for an id the code doesn't know is impossible, because the Inspector only offers existing enum values.

## Tests

`Tests/EditMode/CatalogDatabaseTests.cs` covers:

- With no database, each catalog returns its built-in table, and every saved enum id appears exactly once.
- Exporting the defaults to a database (the same `FromDefs` / `FromProfiles` the editor menu uses) and loading it back reproduces every field.
- An override instance (`ScriptableObject.CreateInstance`) is used: its edited values win, blank text and malformed weights fall back, `None` and duplicate rows are skipped, and missing ids keep their defaults.

One known gap: Unity stores a null array and an empty array the same way. A tech with no completion cost comes back from an asset as `null`, and every reader (`ResearchManager`) already treats null and empty the same.
