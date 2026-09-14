# Dvergr Pieces Patch

**Makes [Tequila's Dvergr Pieces](https://thunderstore.io/c/valheim/p/Tequila/Dvergr_Pieces/)
work on Valheim 1.0.**

This package contains **none of Tequila's files**. It installs his mod as a dependency —
your mod manager will fetch it alongside this one — and repairs it in memory when the game
loads. His assembly is never modified, never patched on disk, and never redistributed.

Everything you actually build comes from his mod. This is one 12 KB plugin that makes it
run again.

---

## What was broken

### 1. The build menu threw on every refresh

Valheim 1.0 refactored `PieceTable`. The field `m_availablePieces` changed from
`List<List<Piece>>` (pieces grouped by category) to `HashSet<Piece>` (a flat set), and the
old per-category list was renamed to `m_availablePiecesByCategory`.

The PieceManager bundled inside Dvergr Pieces still reads `m_availablePieces` as
`List<List<Piece>>`. Because the name still exists with a different type, this is not a
load failure — its patches throw `MissingFieldException` the moment they run, the mod's
category never gets its slot, and `Hud.UpdateBuild` then throws
`ArgumentOutOfRangeException` on every frame the build menu is open. Over a thousand
exceptions in a few minutes of play.

This patch removes those two broken patches from `PieceTable.UpdateAvailable` and installs
equivalents that use the renamed field. Nothing else of PieceManager's is touched.

### 2. The pieces were missing from the new build menu

Valheim 1.0 replaced the old category tab bar with a tag-driven menu (`ByUsagePieceList`)
that filters on a new field, `Piece.m_usage`. Prefabs authored before 1.0 have no usage
tags, so they matched no category and only ever showed under "Show All".

The pieces are now tagged so they appear where you would expect them. `m_usage` is a flags
field, so a piece can sit in more than one category — the chests and the shelf are both
Storage and Furniture, and the pole and beams are both Building and Architecture.

| Menu category | Pieces | Count |
|---|---|---|
| Furniture | bed, table, chair, stool, both chests, shelf | 7 |
| Stairs | metal spiral (left + right), marble spiral (left + right), wooden stairs | 5 |
| Building | black marble block, wooden pole, wooden beam, wooden beam 2x1 | 4 |
| Lighting | black core torch, placeable lantern, wisp torch | 3 |
| Storage | both chests, shelf | 3 |
| Architecture | wooden pole, wooden beam, wooden beam 2x1 | 3 |
| Walls | both dvergr walls | 2 |
| Decor | marble head, vines | 2 |
| Flooring | wooden floor | 1 |
| Doors and Windows | dvergr door | 1 |
| Crafting | fermenter | 1 |

That is 26 prefabs, covering all 24 pieces the mod advertises — the two spiral staircases
each ship as a separate left and right prefab.

This replaces the original's custom "Dvergr" category, which 1.0's new menu can no longer
display.

### 3. The fermenter did not accept newer meads

A bug Tequila acknowledged in his 2.9.0 changelog and never got to. The Dvergr fermenter's
brew list was baked into its asset bundle in 2024, so it refused everything added
afterwards — seven meads in total, from Ashlands and Bog Witch.

Rather than hardcoding a list that would rot on the next update, the fermenter now copies
any missing conversions from the vanilla fermenter at load time, so future meads work
automatically. Its 30% faster fermentation is unchanged.

---

## Installing

Install it with a mod manager and Dvergr Pieces comes with it. If you are installing by
hand, you need **both**: Tequila's `Dvergr Pieces 2.9.0` and this plugin, in
`BepInEx/plugins`.

Load order does not matter — this plugin declares a hard dependency on his and BepInEx
sorts it out. If his mod is missing, this one refuses to load rather than misbehaving.

Existing worlds, saves and configs are unaffected. His mod keeps its own plugin GUID and
its own `Tequila.DvergrPieces.cfg`, which this patch does not touch, so everything you have
already built and every crafting cost you have customised stays exactly as it was.

**On a shared server, install it for everyone.** The build menu fixes are purely visual and
local, so a mixed server is fine there. The fermenter is not: an unpatched client does not
know the added brews, and if it is the one that processes the tap, the fermenter empties
and nothing comes out. Anyone without the patch can still build and use everything the mod
shipped with — only the newer meads need it.

**If you used `Dvergr_Pieces_Reborn`, remove it.** That package bundled a patched copy of
his DLL; this one replaces it and is the reason it was deprecated.

---

## Credits

**Tequila** — the mod itself: every model, texture, icon and piece of design, and every
version up to 2.9.0. None of it is modified or included here.

Tequila's own credits from his page: @Azumatt, @CookieMilk, @GraveBear, @KG, @Marlthon,
@Tjeb, @blaxxun, @GoldenJude, @Yggdrah, @MythikWolf, @GoldenRevolver and everyone at the
Discord now known as the Azumatt Mod Hub.

Thanks to **Azumatt** and **Arrowmaster** for pointing out that the first version of this
went about it the wrong way, and for saying so rather than just having it removed.

His mod has had no update since November 2024, and a February 2026 question on that Discord
about contacting him went unanswered. I have not reached him myself. If Tequila updates the
mod, this patch becomes unnecessary and will be deprecated.

---

## Contact

- **GitHub:** [PatTheEvil/Dvergr_Pieces_Patch](https://github.com/PatTheEvil/Dvergr_Pieces_Patch)
  — issues are the best place, and the full source lives there
- **Discord:** `PatTheEvil`, on the [Azumatt Mod Hub](https://discord.gg/pdHgy6Bsng)
