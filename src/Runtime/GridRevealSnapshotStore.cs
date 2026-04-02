using RevealStarMap.State;

namespace RevealStarMap.Runtime;

internal static class GridRevealSnapshotStore
{
    internal static bool HasActiveSnapshot()
    {
        RevealSnapshotState? state = GetState();
        return state != null && state.HasGridSnapshot && state.GridRevealApplied;
    }

    internal static bool Matches(GridRevealSnapshotKind kind, int worldId)
    {
        RevealSnapshotState? state = GetState();
        return state != null &&
            state.HasGridSnapshot &&
            state.GridRevealApplied &&
            state.GridSnapshotKind == kind &&
            state.GridSnapshotWorldId == worldId;
    }

    internal static GridRevealSnapshotKind GetSnapshotKind()
    {
        return GetState()?.GridSnapshotKind ?? GridRevealSnapshotKind.None;
    }

    internal static int GetSnapshotWorldId()
    {
        return GetState()?.GridSnapshotWorldId ?? -1;
    }

    internal static bool CaptureRect(GridRevealSnapshotKind kind, int worldId, int offsetX, int offsetY, int width, int height)
    {
        RevealSnapshotState? state = GetState();
        if (state == null || !Grid.IsInitialized() || width <= 0 || height <= 0)
        {
            return false;
        }

        int length = width * height;
        var visible = new byte[length];
        var spawnable = new byte[length];

        int index = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int cell = Grid.XYToCell(offsetX + x, offsetY + y);
                visible[index] = Grid.Visible[cell];
                spawnable[index] = Grid.Spawnable[cell];
                index++;
            }
        }

        state.StoreGridSnapshot(kind, worldId, offsetX, offsetY, width, height, visible, spawnable);
        return true;
    }

    internal static void MarkApplied()
    {
        GetState()?.MarkGridRevealApplied();
    }

    internal static bool Restore()
    {
        RevealSnapshotState? state = GetState();
        if (state == null || !state.HasGridSnapshot)
        {
            return false;
        }

        int expectedLength = state.GridSnapshotWidth * state.GridSnapshotHeight;
        if (state.GridVisibleSnapshot.Length != expectedLength || state.GridSpawnableSnapshot.Length != expectedLength)
        {
            state.ClearGridSnapshot();
            return false;
        }

        int index = 0;
        for (int y = 0; y < state.GridSnapshotHeight; y++)
        {
            for (int x = 0; x < state.GridSnapshotWidth; x++)
            {
                int cell = Grid.XYToCell(state.GridSnapshotOffsetX + x, state.GridSnapshotOffsetY + y);
                Grid.Visible[cell] = state.GridVisibleSnapshot[index];
                Grid.Spawnable[cell] = state.GridSpawnableSnapshot[index];
                index++;
            }
        }

        state.ClearGridSnapshot();
        CameraController.Instance?.VisibleArea.Update();
        return true;
    }

    private static RevealSnapshotState? GetState()
    {
        return SaveGame.Instance == null ? null : SaveGame.Instance.GetComponent<RevealSnapshotState>();
    }
}
