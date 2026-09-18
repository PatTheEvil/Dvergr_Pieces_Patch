// Puts the Dvergr pieces under a category of their own in the 1.0 build menu,
// in addition to the vanilla categories they are tagged with.
//
// ByUsagePieceList does not hardcode its tag list. Its constructor fills two
// instance arrays -- m_usageTags from Enum.GetValues(typeof(Piece.UsageTagFlags))
// and m_usageTagDisplayNames from each value's DisplayNameAttribute -- and the
// rest of the menu reads only those arrays:
//
//   UpdateAvailableTags   ORs m_usage over every available piece, then lists the
//                         entries of m_usageTags present in that mask
//   GetAvailablePiecesWithTag  filters by piece.m_usage.HasFlag(GetTagById(id))
//
// So appending one entry to those two arrays is enough for a new tag to appear and
// to work. Nothing in the enum or the BCL is touched.
//
// m_usage is a [Flags] Int32, and vanilla uses bits 0-19, so a piece can carry both
// its natural tag and ours. The bit is allocated from what the instance actually
// holds rather than hardcoded, so a tag added by another mod (Jotunn can do this
// since 2.30.1) does not collide.
using System;
using HarmonyLib;

namespace DvergrPiecesPatch
{
    internal static class CustomCategory
    {
        internal const string DisplayName = "Dvergr";

        static Piece.UsageTagFlags flag;

        /// The tag, or 0 while the build menu has not been constructed yet.
        internal static Piece.UsageTagFlags Flag
        {
            get { return Plugin.CustomCategory.Value ? flag : 0; }
        }

        static readonly AccessTools.FieldRef<ByUsagePieceList, Piece.UsageTagFlags[]> TagsOf =
            AccessTools.FieldRefAccess<ByUsagePieceList, Piece.UsageTagFlags[]>("m_usageTags");

        static readonly AccessTools.FieldRef<ByUsagePieceList, string[]> NamesOf =
            AccessTools.FieldRefAccess<ByUsagePieceList, string[]>("m_usageTagDisplayNames");

        internal static void Register(ByUsagePieceList list)
        {
            if (!Plugin.CustomCategory.Value) return;

            Piece.UsageTagFlags[] tags = TagsOf(list);
            string[] names = NamesOf(list);
            if (tags == null || names == null || tags.Length != names.Length) return;

            if (flag == 0)
            {
                int used = 0;
                foreach (Piece.UsageTagFlags t in tags) used |= (int)t;

                int bit = 1;
                while ((used & bit) != 0)
                {
                    bit <<= 1;
                    if (bit == 0)
                    {
                        Plugin.Log.LogWarning("No free usage tag bit left - skipping the Dvergr category");
                        return;
                    }
                }
                flag = (Piece.UsageTagFlags)bit;
            }

            foreach (Piece.UsageTagFlags t in tags)
                if (t == flag) return;   // already registered on this list

            Array.Resize(ref tags, tags.Length + 1);
            Array.Resize(ref names, names.Length + 1);
            tags[tags.Length - 1] = flag;
            names[names.Length - 1] = DisplayName;

            TagsOf(list) = tags;
            NamesOf(list) = names;

            Plugin.Log.LogInfo("Registered build menu category '" + DisplayName
                               + "' as usage tag " + (int)flag);
        }
    }

    // Late, so that a tag added by another mod is already in the array when the
    // free bit is picked.
    [HarmonyPatch(typeof(ByUsagePieceList), MethodType.Constructor, typeof(string))]
    internal static class ByUsagePieceList_Ctor_Patch
    {
        [HarmonyPriority(Priority.Last)]
        static void Postfix(ByUsagePieceList __instance) => CustomCategory.Register(__instance);
    }
}
