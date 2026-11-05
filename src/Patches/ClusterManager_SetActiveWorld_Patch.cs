using HarmonyLib;
using RevealStarMap.Runtime;

namespace RevealStarMap.Patches;

[HarmonyPatch(typeof(ClusterManager), nameof(ClusterManager.SetActiveWorld))]
public static class ClusterManager_SetActiveWorld_Patch
{
    // Runs before the change is announced, while the leaving world's camera is still current.
    internal static void Prefix(int worldIdx, out RevealSessionCoordinator.SavedView? __state)
    {
        __state = RevealSessionCoordinator.BeforeActiveWorldChange(worldIdx);
    }

    // Runs after UserNavigation has recorded the leaving world.
    internal static void Postfix(RevealSessionCoordinator.SavedView? __state)
    {
        RevealSessionCoordinator.AfterActiveWorldChange(__state);
    }
}
