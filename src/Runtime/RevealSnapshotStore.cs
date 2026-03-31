using System.Collections.Generic;
using HarmonyLib;
using RevealStarMap.State;

namespace RevealStarMap.Runtime;

internal static class RevealSnapshotStore
{
    private static readonly AccessTools.FieldRef<ClusterFogOfWarManager.Instance, Dictionary<AxialI, float>> RevealPointsRef =
        AccessTools.FieldRefAccess<ClusterFogOfWarManager.Instance, Dictionary<AxialI, float>>("m_revealPointsByCell");

    internal static bool Capture()
    {
        var fog = GetFog();
        RevealSnapshotState? snapshotState = GetSnapshotState();
        if (fog == null || snapshotState == null)
        {
            return false;
        }

        Dictionary<AxialI, float> source = GetCurrentRevealPoints(fog);
        snapshotState.Store(source);
        return true;
    }

    internal static bool HasSnapshot()
    {
        RevealSnapshotState? snapshotState = GetSnapshotState();
        return snapshotState != null && snapshotState.HasSnapshot;
    }

    internal static bool ShouldRestore()
    {
        RevealSnapshotState? snapshotState = GetSnapshotState();
        return snapshotState != null && snapshotState.HasSnapshot && snapshotState.RevealApplied;
    }

    internal static void MarkRevealApplied()
    {
        GetSnapshotState()?.MarkRevealApplied();
    }

    internal static bool Restore()
    {
        var fog = GetFog();
        RevealSnapshotState? snapshotState = GetSnapshotState();
        if (fog == null || snapshotState == null || !snapshotState.HasSnapshot)
        {
            return false;
        }

        ref Dictionary<AxialI, float> revealPoints = ref RevealPointsRef(fog);
        revealPoints = snapshotState.RestoreSnapshot();
        fog.UpdateRevealedCellsFromDiscoveredWorlds();
        snapshotState.ClearSnapshot();
        RefreshClusterMap();
        return true;
    }

    private static ClusterFogOfWarManager.Instance? GetFog()
    {
        return SaveGame.Instance == null ? null : SaveGame.Instance.GetSMI<ClusterFogOfWarManager.Instance>();
    }

    private static RevealSnapshotState? GetSnapshotState()
    {
        return SaveGame.Instance == null ? null : SaveGame.Instance.GetComponent<RevealSnapshotState>();
    }

    private static Dictionary<AxialI, float> GetCurrentRevealPoints(ClusterFogOfWarManager.Instance fog)
    {
        Dictionary<AxialI, float>? source = RevealPointsRef(fog);
        return source == null ? new Dictionary<AxialI, float>() : new Dictionary<AxialI, float>(source);
    }

    internal static void RefreshClusterMap()
    {
        ClusterMapScreen.Instance?.Trigger(1980521255);
    }
}
