using HarmonyLib;
using RevealStarMap.Runtime;
using RevealStarMap.State;

namespace RevealStarMap.Patches;

[HarmonyPatch(typeof(SaveGame))]
public static class SaveGame_OnSpawn_Patch
{
    // SaveLoadRoot only deserializes components already present on the prefab.
    [HarmonyPatch("OnPrefabInit")]
    [HarmonyPostfix]
    public static void OnPrefabInitPostfix(SaveGame __instance)
    {
        if (DlcManager.FeatureClusterSpaceEnabled())
            __instance.gameObject.AddOrGet<RevealSnapshotState>();
    }

    [HarmonyPatch("OnSpawn")]
    [HarmonyPostfix]
    public static void Postfix(SaveGame __instance)
    {
        if (!DlcManager.FeatureClusterSpaceEnabled())
        {
            return;
        }

        RevealStarMapSettingsManager.Reload();
        __instance.gameObject.AddOrGet<RevealSnapshotState>();
        __instance.gameObject.AddOrGet<RevealStarMapController>();
    }
}
