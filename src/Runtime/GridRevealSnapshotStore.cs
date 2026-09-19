using RevealStarMap.State;

namespace RevealStarMap.Runtime;

internal static class GridRevealSnapshotStore
{
    internal static bool HasActiveSnapshot()
    {
        RevealSnapshotState? state = GetState();
        return state != null && state.HasGridSnapshot && state.GridRevealApplied;
    }

    internal static GridRevealSnapshotKind GetSnapshotKind()
    {
        return GetState()?.GridSnapshotKind ?? GridRevealSnapshotKind.None;
    }

    internal static int GetSnapshotWorldId()
    {
        return GetState()?.GridSnapshotWorldId ?? -1;
    }

    internal static bool Restore()
    {
        RevealSnapshotState? state = GetState();
        if (state == null || !state.HasGridSnapshot || !Grid.IsInitialized())
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
                int gridX = state.GridSnapshotOffsetX + x;
                int gridY = state.GridSnapshotOffsetY + y;
                if (gridX < 0 || gridY < 0 || gridX >= Grid.WidthInCells || gridY >= Grid.HeightInCells)
                {
                    index++;
                    continue;
                }

                int cell = Grid.XYToCell(gridX, gridY);
                if (!Grid.IsValidCell(cell))
                {
                    index++;
                    continue;
                }

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
