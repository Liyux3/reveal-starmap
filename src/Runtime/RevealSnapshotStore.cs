using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RevealStarMap.State;

namespace RevealStarMap.Runtime;

internal static class RevealSnapshotStore
{
    private static readonly AccessTools.FieldRef<ClusterFogOfWarManager.Instance, Dictionary<AxialI, float>> RevealPointsRef =
        AccessTools.FieldRefAccess<ClusterFogOfWarManager.Instance, Dictionary<AxialI, float>>("m_revealPointsByCell");

    private static readonly MethodInfo? UpdateVisMethod =
        AccessTools.Method(typeof(ClusterMapScreen), "UpdateVis", new[] { typeof(bool) });

    internal static bool ShouldRestore()
    {
        RevealSnapshotState? snapshotState = GetSnapshotState();
        return snapshotState != null && snapshotState.HasSnapshot && snapshotState.RevealApplied;
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
        snapshotState.ClearSnapshot();
        RefreshClusterMap();
        return true;
    }

    internal static ClusterFogOfWarManager.Instance? GetFog()
    {
        return SaveGame.Instance == null ? null : SaveGame.Instance.GetSMI<ClusterFogOfWarManager.Instance>();
    }

    internal static Dictionary<AxialI, float> PrepareSurvey(ClusterFogOfWarManager.Instance fog,
        IEnumerable<AxialI> cells, out int changed)
    {
        return StarMapSurvey.Prepare(RevealPointsRef(fog), cells,
            TUNING.ROCKETRY.CLUSTER_FOW.POINTS_TO_REVEAL, out changed);
    }

    internal static void CommitSurvey(ClusterFogOfWarManager.Instance fog,
        Dictionary<AxialI, float> surveyed)
    {
        RevealPointsRef(fog) = surveyed;
        // One batch notification updates telescope targets and cached routes. A null location
        // avoids emitting a discovery popup (or revealing terrain) for every asteroid.
        Game.Instance.Trigger((int)GameHashes.ClusterFogOfWarRevealed);
    }

    private static RevealSnapshotState? GetSnapshotState()
    {
        return SaveGame.Instance == null ? null : SaveGame.Instance.GetComponent<RevealSnapshotState>();
    }

    internal static void RefreshClusterMap()
    {
        ClusterMapScreen? screen = ClusterMapScreen.Instance;
        if (screen == null || UpdateVisMethod == null)
        {
            return;
        }

        try
        {
            UpdateVisMethod.Invoke(screen, new object[] { false });
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"{RevealStarMapMod.Tag} Could not refresh the starmap preview. {exception}");
        }
    }
}
