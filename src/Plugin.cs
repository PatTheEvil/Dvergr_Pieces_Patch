// Dvergr Pieces - Valheim 1.0 compatibility patch
//
// This plugin contains none of Tequila's work. It depends on his original
// Dvergr Pieces package and repairs it in memory at load time, so his assembly
// is never modified on disk and never redistributed.
//
// Three things are repaired; see PieceTableFix, UsageTags and FermenterSync.
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace DvergrPiecesPatch
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(DvergrPiecesGUID, BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID    = "PatTheEvil.DvergrPiecesPatch";
        public const string NAME    = "Dvergr Pieces - Valheim 1.0 Patch";
        public const string VERSION = "1.0.0";

        // Tequila's plugin. HardDependency means BepInEx loads his first and
        // refuses to load this one at all if his package is missing.
        public const string DvergrPiecesGUID = "Tequila.DvergrPieces";

        internal static ManualLogSource Log;

        void Awake()
        {
            Log = Logger;

            var harmony = new Harmony(GUID);

            // Must run before anything calls PieceTable.UpdateAvailable, i.e. before
            // a player equips a hammer. Awake is comfortably early.
            PieceTableFix.Apply(harmony);

            harmony.PatchAll();
        }
    }
}
