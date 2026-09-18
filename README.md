# Dvergr Pieces Patch

Makes [Tequila's Dvergr Pieces](https://thunderstore.io/c/valheim/p/Tequila/Dvergr_Pieces/)
run on **Valheim 1.0**.

[Thunderstore package](https://thunderstore.io/c/valheim/p/PatTheEvil/Dvergr_Pieces_Patch/) ·
[Report a bug](../../issues)

**None of Tequila's work is here or in the package.** His mod is a Thunderstore dependency
and is repaired in memory at load time — his assembly is never modified, patched on disk,
or redistributed. This repo holds only the plugin that does the repairing.

---

## What it fixes

| # | Problem | Where |
|---|---|---|
| 1 | `PieceTable.m_availablePieces` changed type in 1.0, so PieceManager's patches threw `MissingFieldException`, then `Hud.UpdateBuild` threw `ArgumentOutOfRangeException` every frame | `src/PieceTableFix.cs` |
| 2 | 1.0's build menu filters on the new `Piece.m_usage`; pre-1.0 prefabs have no tags, so they only appeared under "Show All" | `src/UsageTags.cs` |
| 3 | The fermenter refused meads added after 2024 — a bug Tequila noted in 2.9.0 and never fixed | `src/FermenterSync.cs` |
| 4 | The mod's own "Dvergr" category disappeared, because 1.0's menu no longer takes categories from piece tables | `src/CustomCategory.cs` |

Full detail in [`package/README.md`](package/README.md), which is what ships on Thunderstore.

(4) is not a break so much as a restoration, and it is optional — a config entry turns it
off. `ByUsagePieceList` fills `m_usageTags` and `m_usageTagDisplayNames` in its constructor
and the rest of the menu reads only those two arrays, so a category is one appended entry
each. The bit is allocated from what the array actually holds rather than hardcoded, so it
will not collide with a category registered by another mod.

### How the first one works

The interesting one is (1), because it has to repair someone else's compiled code without
touching it.

`PieceManager.PiecePrefabManager` installs a prefix and a postfix on
`PieceTable.UpdateAvailable`. Both read `m_availablePieces` expecting `List<List<Piece>>`.
In 1.0 that name still exists but holds a `HashSet<Piece>`, and the list it used to be was
renamed `m_availablePiecesByCategory`. A signature mismatch like that is not caught at load
— it throws when the method is JIT-compiled.

So instead of rewriting his assembly, this plugin waits until his mod has loaded (a hard
`BepInDependency` guarantees the order), then calls `Harmony.Unpatch` to remove those two
patches from `PieceTable.UpdateAvailable` and installs equivalents that use the renamed
field. His broken methods are never called again, so they are never compiled, so they never
throw. Everything else PieceManager does — including the `Enum.GetValues` patch that makes
its custom category exist — is left alone.

---

## Configuration

`BepInEx/config/PatTheEvil.DvergrPiecesPatch.cfg`, created on first run. Both take effect
after a restart.

| Setting | Default | Effect |
|---|---|---|
| `Dvergr category` | `true` | Adds the **Dvergr** category to the build menu. Cosmetic and local; does not have to match between players. |
| `Fermenter meads` | `true` | Lets the Dvergr fermenter accept every mead the vanilla fermenter does. **Turn it off on a server where not everyone has the patch.** |

### Everyone on a server needs this, the dedicated server included

The build menu fixes are local and cosmetic, so a mixed server is fine there. The fermenter
is not.

`Fermenter.Interact` sends `RPC_Tap` to whoever owns the fermenter's ZDO without claiming
ownership first. `RPC_Tap` clears the stored contents immediately and schedules
`DelayedTap`, which spawns nothing when `GetItemConversion` finds no match — so if the peer
that owns the fermenter lacks the patch, tapping a newer mead destroys it silently.

Ownership is not who built it and not necessarily who clicked it: `ZDOMan` gives it to a
peer that has the object in its active area and only reassigns once that peer leaves, and a
dedicated server can hold it. So a second player standing at your fermenter can be the one
who processes your tap, which means having the patch yourself is not enough.

This mismatch is one the patch creates — before it nobody could ferment those meads, so
nobody could lose one. `Fermenter meads = false` removes the risk entirely and leaves the
build menu fixes working. [`package/README.md`](package/README.md) says the same at more
length, for players.

---

## Building

Requirements: Windows, Valheim installed, BepInEx installed in any mod manager profile (the
build borrows `BepInEx.dll`, `0Harmony.dll` and `Mono.Cecil.dll` from it), and a Roslyn
`csc.exe` — the Visual Studio ".NET desktop development" workload provides one.

```powershell
.\build.ps1
```

It locates Valheim, BepInEx and the compiler on its own; pass `-Managed`, `-BepInExCore` or
`-Csc` if autodetection misses. Output lands in `dist/` as the Thunderstore zip.

### verify-compat.ps1

`tools/verify-compat.ps1` checks every reference a mod assembly makes into
`assembly_valheim.dll` against the installed game. Run it after each Valheim update, before
launching. It reports two kinds of breakage:

- **direct IL references** — a renamed member shows as MISSING; one that kept its name but
  changed type or signature shows as CHANGED. The second kind is what broke Dvergr Pieces:
  the mod still loads, then throws at runtime.
- **string-based reflection** (`AccessTools.Field`, `DeclaredMethod`, …) — invisible to
  compilation, because a stale name just returns null and the caller silently does nothing.

It works on any mod, not just this one.

---

## Attribution and licensing

Every model, texture and piece of design in Dvergr Pieces is **Tequila's**, and none of it
is included here or altered. This repo is only the patch.

His mod has had no update since November 2024, and a February 2026 question on the Azumatt
Mod Hub Discord about contacting him went unanswered. I have not reached him myself. If he
updates the mod, this patch becomes unnecessary and will be deprecated.

The code here is released under the MIT licence. Take it — the same approach fixes any
PieceManager-based mod that 1.0 broke, and there are a lot of them.

---

## History

The first version of this, published as `Dvergr_Pieces_Reborn`, shipped Tequila's compiled
DLL with five IL field references rewritten. **Azumatt** and **Arrowmaster** pointed out
that redistributing another author's copyrighted assembly is not acceptable even unmodified,
and that the right shape is to depend on the original and patch it at runtime. They were
right; that package is deprecated and this one replaces it.

---

## Contact

- **Discord:** `PatTheEvil`, on the [Azumatt Mod Hub](https://discord.gg/pdHgy6Bsng)
- **Bugs:** [open an issue](../../issues)
