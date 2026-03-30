using HarmonyLib;
using RevealStarMap.Runtime;

namespace RevealStarMap.Patches;

[HarmonyPatch(typeof(SaveGame), "OnSpawn")]
public static class SaveGame_OnSpawn_Patch
{
    public static void Postfix(SaveGame __instance)
    {
        if (!DlcManager.FeatureClusterSpaceEnabled())
        {
            return;
        }

        __instance.gameObject.AddOrGet<RevealStarMapController>();
    }
}
