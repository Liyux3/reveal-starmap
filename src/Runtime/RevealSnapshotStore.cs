using System.Collections.Generic;
using HarmonyLib;

namespace RevealStarMap.Runtime;

internal static class RevealSnapshotStore
{
    private static readonly AccessTools.FieldRef<ClusterFogOfWarManager.Instance, Dictionary<AxialI, float>> RevealPointsRef =
        AccessTools.FieldRefAccess<ClusterFogOfWarManager.Instance, Dictionary<AxialI, float>>("m_revealPointsByCell");

    private static Dictionary<AxialI, float>? lastSnapshot;

    internal static void Capture()
    {
        if (SaveGame.Instance == null)
        {
            return;
        }

        ClusterFogOfWarManager.Instance fog = SaveGame.Instance.GetSMI<ClusterFogOfWarManager.Instance>();
        var source = RevealPointsRef(fog);
        lastSnapshot = source == null ? new Dictionary<AxialI, float>() : new Dictionary<AxialI, float>(source);
    }

    internal static bool HasSnapshot()
    {
        return lastSnapshot != null;
    }
}
