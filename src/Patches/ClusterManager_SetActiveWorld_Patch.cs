using HarmonyLib;
using RevealStarMap.Runtime;

namespace RevealStarMap.Patches;

[HarmonyPatch(typeof(ClusterManager), nameof(ClusterManager.SetActiveWorld))]
public static class ClusterManager_SetActiveWorld_Patch
{
    // Runs before the change is announced, while the leaving world's camera is still current.
    public static void Prefix(int worldIdx)
    {
        RevealSessionCoordinator.BeforeActiveWorldChange(worldIdx);
    }
}
