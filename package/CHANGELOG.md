# Changelog

## 1.1.0

- The pieces now also appear together under a **Dvergr** category in the build menu, the
  way the original mod had them before Valheim 1.0. It is an extra tag rather than a move,
  so each piece still shows under its vanilla category as well.
- Added a config option, `Dvergr category`, to turn that off.
- Added a config option, `Fermenter meads`, to turn the fermenter brew sync off. On a
  server where not everyone has the patch, an unpatched client cannot tap a newer mead and
  tapping destroys it - a mismatch this patch is what creates, so there is now a way out of
  it that does not mean uninstalling.

## 1.0.0

First release of the patch.

- Repairs PieceManager's `PieceTable.UpdateAvailable` prefix/postfix, which broke on
  Valheim 1.0 when `m_availablePieces` changed from `List<List<Piece>>` to
  `HashSet<Piece>` and the old per-category list was renamed to
  `m_availablePiecesByCategory`
- Adds Valheim 1.0 `Piece.m_usage` tags so the pieces appear in the new build menu's
  categories instead of only under "Show All"
- Keeps the Dvergr Fermenter's brew list in sync with the vanilla fermenter, so it
  accepts every mead including the seven added since Ashlands, and will pick up future
  ones automatically. This fixes a known issue from Dvergr Pieces 2.9.0.

---

## Note on Dvergr_Pieces_Reborn

This package replaces `PatTheEvil-Dvergr_Pieces_Reborn`, which has been deprecated.

That package redistributed Tequila's compiled assembly with the fix applied to it.
Azumatt pointed out that bundling another author's copyrighted DLL is not acceptable
even unmodified, and that the right shape is to depend on the original and patch it at
runtime. He was right. This package does that instead, and contains no files of his.
