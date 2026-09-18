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

**All of them also appear together under a "Dvergr" category**, the way the original mod
had them before 1.0. Because `m_usage` is a flags field, that is an extra bit rather than a
move: the bed is under Furniture *and* under Dvergr. It can be turned off — see
Configuration.

`ByUsagePieceList` builds its tag list into two instance arrays at construction, and the
rest of the menu reads only those, so the category is added by appending one entry to each
— no change to the enum and nothing patched in the BCL. The bit is picked from what the
array actually holds rather than hardcoded, so a category added by another mod (Jötunn can
do this since 2.30.1) will not collide with it.

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

**If you used `Dvergr_Pieces_Reborn`, remove it.** That package bundled a patched copy of
his DLL; this one replaces it and is the reason it was deprecated.

### On a server, everyone needs it — including the server itself

> **In short:** if even one person on your server is missing the patch, a newer mead can be
> destroyed when the Dvergr fermenter is tapped — including one you brewed yourself, on
> your own fermenter, with the patch installed. Either put it on every client and the
> server, or set `Fermenter meads = false`. Nothing else in the mod is affected.

| Where | Needed? | Why |
|---|---|---|
| Every player | **yes** | any of them can be the one that processes a tap |
| The dedicated server | **yes** | it can own the fermenter when no player has claimed it |

The build menu fixes are purely local and cosmetic, so a player without the patch just sees
the pieces the way 1.0 left them. The fermenter is the part that matters, and the reason it
is not enough to have the patch yourself is worth spelling out.

`Fermenter.Interact` sends `RPC_Tap` to whoever **owns the fermenter's ZDO**, and does not
claim ownership first. `RPC_Tap` clears the stored contents immediately and schedules
`DelayedTap`, which spawns nothing at all if its own copy of the brew list does not know
the item. So if the peer that owns that fermenter lacks the patch, tapping a newer mead
destroys it silently — the animation plays and nothing comes out.

The owner is not who built it, and not necessarily who clicked it. `ZDOMan` hands ownership
to a peer that has the object in its active area, and only reassigns it once the current
owner leaves — so ownership is sticky. A second player standing at your fermenter can be
the one who processes your tap, and a dedicated server can own it when no player has
claimed it. Having the patch yourself is therefore not enough.

This mismatch is one the patch itself creates: before it, nobody could ferment those meads,
so nobody could lose one. **If you cannot get it onto every client and the server, set
`Fermenter meads = false`** (see Configuration below). That leaves the fermenter exactly as
Tequila shipped it, removes the risk entirely, and the build menu fixes keep working.

---

## Configuration

`BepInEx/config/PatTheEvil.DvergrPiecesPatch.cfg`, created on first run. Both settings take
effect after a restart.

| Setting | Default | What it does |
|---|---|---|
| `Dvergr category` | `true` | Adds a **Dvergr** category to the build menu listing every piece of the mod. Purely cosmetic and purely local — it does not have to match between players. Turn it off to have the pieces only in the vanilla categories. |
| `Fermenter meads` | `true` | Lets the Dvergr fermenter accept every mead the vanilla fermenter does. **Turn it off on a server where not everyone has the patch** — see above. |

Tequila's own config, `Tequila.DvergrPieces.cfg`, is separate and untouched. Crafting costs,
stations and piece categories still live there.

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
