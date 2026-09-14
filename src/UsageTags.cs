// Valheim 1.0 replaced the old PieceCategory tab bar with a tag-driven menu
// (ByUsagePieceList) which filters on a new field, Piece.m_usage. Prefabs authored
// before 1.0 have m_usage == 0, match no tag, and so only ever appear under
// "Show All". This assigns sensible tags to the Dvergr pieces.
//
// Tagging is lazy: the first time a piece table containing them is refreshed. Once
// m_usage is non-zero a piece is skipped, so after the first pass the cost is a
// dictionary miss per piece.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace DvergrPiecesPatch
{
    using Tag = Piece.UsageTagFlags;

    internal static class UsageTags
    {
        internal static readonly Dictionary<string, Tag> Map = new Dictionary<string, Tag>
        {
            // storage
            { "piece_chest_dvergr_tq",          Tag.Storage | Tag.Furniture },
            { "piece_chest_dvergr_alt_tq",      Tag.Storage | Tag.Furniture },
            { "teq_dv_dvergr_shelf",            Tag.Storage | Tag.Furniture },
            // lighting
            { "blackCoreTorch_tq",              Tag.Lighting },
            { "teq_dv_dvergr_lantern",          Tag.Lighting },
            { "teq_dv_dvergr_wisp_torch",       Tag.Lighting },
            // crafting
            { "teq_dv_ferm_fermenter",          Tag.Crafting },
            // doors
            { "teq_dvergr_door",                Tag.Doors },
            // decor
            { "teq_dvergrhead_crown",           Tag.Decor },
            { "teq_dv_vines",                   Tag.Decor },
            // furniture
            { "teq_dv_dvergr_bed",              Tag.Furniture },
            { "teq_dv_dvergr_table",            Tag.Furniture },
            { "teq_dv_dvergr_chair",            Tag.Furniture },
            { "teq_dv_dvergr_stool",            Tag.Furniture },
            // stairs
            { "teq_dvergr_metal_stairs_left",   Tag.Stairs },
            { "teq_dvergr_metal_stairs_right",  Tag.Stairs },
            { "teq_dv_bm_spiral_left",          Tag.Stairs },
            { "teq_dv_bm_spiral_right",         Tag.Stairs },
            { "teq_dv_wood_stair",              Tag.Stairs },
            // structure
            { "teq_dv_wood_metal_wall",         Tag.Wall },
            { "teq_dv_wood_metal_wall_alt",     Tag.Wall },
            { "teq_dv_dvergr_wood_floor",       Tag.Floor },
            { "teq_metal_marble_2x2",           Tag.Building },
            { "teq_dv_wood_metal_pole",         Tag.Building | Tag.Architecture },
            { "teq_dv_dvergr_wood_beam",        Tag.Building | Tag.Architecture },
            { "teq_dv_dvergr_wood_beam_med",    Tag.Building | Tag.Architecture },
        };

        static int tagged;
        static bool reported;

        internal static void Apply(PieceTable table)
        {
            if (table == null || table.m_pieces == null) return;

            foreach (GameObject go in table.m_pieces)
            {
                if (go == null) continue;

                string name = go.name;
                int clone = name.IndexOf("(Clone)");
                if (clone >= 0) name = name.Substring(0, clone);

                Tag tag;
                if (!Map.TryGetValue(name, out tag)) continue;

                Piece piece = go.GetComponent<Piece>();
                if (piece == null || piece.m_usage != 0) continue;

                piece.m_usage = tag;
                tagged++;
            }

            if (tagged > 0 && !reported)
            {
                reported = true;
                Plugin.Log.LogInfo("Tagged " + tagged + " Dvergr piece(s) for the 1.0 build menu");
            }
        }
    }

    [HarmonyPatch(typeof(PieceTable), "UpdateAvailable")]
    internal static class PieceTable_UpdateAvailable_Tagging
    {
        static void Prefix(PieceTable __instance) => UsageTags.Apply(__instance);
    }
}
