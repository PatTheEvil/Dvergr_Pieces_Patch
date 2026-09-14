// Repairs PieceManager's PieceTable patches for Valheim 1.0, in memory.
//
// Valheim 1.0 refactored PieceTable:
//
//   before 1.0   List<List<Piece>> m_availablePieces           (pieces per category)
//   in 1.0       HashSet<Piece>    m_availablePieces           (flat set, new meaning)
//                List<List<Piece>> m_availablePiecesByCategory (the old field, renamed)
//
// The PieceManager bundled inside Dvergr Pieces still reads m_availablePieces as
// List<List<Piece>>. The name still exists with a different type, so this is not a
// load error: its UpdateAvailable_Prefix/_Postfix throw MissingFieldException the
// moment they are JIT-compiled, the mod's custom category never gets its slot in
// the per-category list, and Hud.UpdateBuild then throws ArgumentOutOfRangeException
// on every frame the build menu is open.
//
// Rather than rewriting his assembly, we remove those two patches from
// PieceTable.UpdateAvailable and install equivalents that use the renamed field.
// His broken methods are never called again, so their bodies are never compiled.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace DvergrPiecesPatch
{
    internal static class PieceTableFix
    {
        const string PieceManagerType = "PieceManager.PiecePrefabManager";

        static AccessTools.FieldRef<PieceTable, List<List<Piece>>> byCategory;

        internal static void Apply(Harmony harmony)
        {
            Assembly dvergr = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "DvergrPieces");

            if (dvergr == null)
            {
                Plugin.Log.LogError("DvergrPieces assembly not found - nothing patched.");
                return;
            }

            Type pm = dvergr.GetType(PieceManagerType);
            if (pm == null)
            {
                Plugin.Log.LogError(PieceManagerType + " not found in DvergrPieces - nothing patched.");
                return;
            }

            // Its static constructor is what installs the patches. It has normally run
            // by now (his Awake registers pieces), but force it so ordering cannot bite.
            RuntimeHelpers.RunClassConstructor(pm.TypeHandle);

            MethodBase target  = AccessTools.DeclaredMethod(typeof(PieceTable), "UpdateAvailable");
            MethodInfo theirPre  = AccessTools.DeclaredMethod(pm, "UpdateAvailable_Prefix");
            MethodInfo theirPost = AccessTools.DeclaredMethod(pm, "UpdateAvailable_Postfix");

            if (target == null || theirPre == null || theirPost == null)
            {
                Plugin.Log.LogError("Could not resolve PieceTable.UpdateAvailable or PieceManager's patches "
                                    + "- nothing patched.");
                return;
            }

            byCategory = AccessTools.FieldRefAccess<PieceTable, List<List<Piece>>>("m_availablePiecesByCategory");

            harmony.Unpatch(target, theirPre);
            harmony.Unpatch(target, theirPost);

            harmony.Patch(target,
                prefix:  new HarmonyMethod(typeof(PieceTableFix), nameof(Prefix)),
                postfix: new HarmonyMethod(typeof(PieceTableFix), nameof(Postfix)));

            Plugin.Log.LogInfo("Replaced PieceManager's UpdateAvailable prefix/postfix with 1.0-compatible ones");
        }

        // PieceManager registers a custom category beyond the vanilla range, so the
        // per-category list has to be grown to match. His Enum.GetValues patch is what
        // makes the enum report the extra entries, and it is left untouched.
        static void Prefix(PieceTable __instance)
        {
            List<List<Piece>> cats = byCategory(__instance);
            if (cats == null || cats.Count == 0) return;

            int want = Enum.GetValues(typeof(Piece.PieceCategory)).Length - 1;
            while (cats.Count < want) cats.Add(new List<Piece>());
        }

        // The selection cursors are indexed by category, so they have to grow too.
        static void Postfix(PieceTable __instance)
        {
            List<List<Piece>> cats = byCategory(__instance);
            if (cats == null) return;

            Array.Resize(ref __instance.m_selectedPiece, cats.Count);
            Array.Resize(ref __instance.m_lastSelectedPiece, cats.Count);
        }
    }
}
