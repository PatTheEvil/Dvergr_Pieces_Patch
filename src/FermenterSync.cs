// Dvergr Fermenter - keep its brew list in sync with the vanilla fermenter.
//
// The mod's fermenter carries a Fermenter.m_conversion list baked into its asset
// bundle in 2024. Meads added after that (the Bog Witch meadbases, and anything
// newer) are missing from it, so the Dvergr fermenter silently refuses them --
// a bug the original author acknowledged in the 2.9.0 changelog and never fixed.
//
// Instead of hardcoding item names, which would rot again on the next update,
// we union in every conversion the vanilla fermenter knows about. The mod's own
// m_fermentationDuration is left alone, so the piece keeps being 30% faster.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace DvergrPiecesPatch
{
    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class ZNetScene_Awake_Patch
    {
        const string ModFermenter    = "teq_dv_ferm_fermenter";
        const string VanillaFermenter = "fermenter";

        static void Postfix(ZNetScene __instance)
        {
            if (__instance == null) return;
            if (!Plugin.FermenterMeads.Value)
            {
                Plugin.Log.LogInfo("Fermenter mead sync disabled by config - brew list left as-is");
                return;
            }

            Fermenter mine = FindFermenter(__instance, ModFermenter);
            if (mine == null) return;

            Fermenter vanilla = FindFermenter(__instance, VanillaFermenter) ?? BestVanillaGuess(__instance);
            if (vanilla == null)
            {
                Plugin.Log.LogWarning("Vanilla fermenter not found - Dvergr fermenter brew list left as-is");
                return;
            }

            if (mine.m_conversion == null) mine.m_conversion = new List<Fermenter.ItemConversion>();

            HashSet<string> known = new HashSet<string>();
            foreach (Fermenter.ItemConversion c in mine.m_conversion)
                if (c != null && c.m_from != null) known.Add(c.m_from.name);

            List<string> added = new List<string>();
            foreach (Fermenter.ItemConversion c in vanilla.m_conversion)
            {
                if (c == null || c.m_from == null || c.m_to == null) continue;
                if (known.Contains(c.m_from.name)) continue;

                mine.m_conversion.Add(new Fermenter.ItemConversion
                {
                    m_from = c.m_from,
                    m_to = c.m_to,
                    m_producedItems = c.m_producedItems
                });
                known.Add(c.m_from.name);
                added.Add(c.m_from.name);
            }

            if (added.Count > 0)
                Plugin.Log.LogInfo("Dvergr fermenter: added " + added.Count + " missing brew(s) from the vanilla fermenter: "
                                   + string.Join(", ", added.ToArray()));
            else
                Plugin.Log.LogInfo("Dvergr fermenter: brew list already complete ("
                                   + mine.m_conversion.Count + " conversions)");
        }

        static Fermenter FindFermenter(ZNetScene scene, string prefabName)
        {
            GameObject go = scene.GetPrefab(prefabName);
            return go == null ? null : go.GetComponent<Fermenter>();
        }

        // Fallback if the vanilla prefab is ever renamed: the non-Dvergr fermenter
        // prefab that knows about the most brews.
        static Fermenter BestVanillaGuess(ZNetScene scene)
        {
            Fermenter best = null;
            foreach (GameObject go in scene.m_prefabs)
            {
                if (go == null || go.name == ModFermenter) continue;
                Fermenter f = go.GetComponent<Fermenter>();
                if (f == null || f.m_conversion == null) continue;
                if (best == null || f.m_conversion.Count > best.m_conversion.Count) best = f;
            }
            return best;
        }
    }
}
