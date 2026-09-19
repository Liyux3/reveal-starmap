using System;
using System.Collections.Generic;

namespace RevealStarMap.Runtime;

internal static class RevealStarMapRuntime
{
    internal static void TriggerStarmapHotkeyAction()
    {
        if (SaveGame.Instance == null || Game.Instance == null || Game.Instance.IsLoading())
            return;

        // Always allow an active preview to end, even after changing the global action.
        if (RevealSessionCoordinator.IsStarMapPreviewActive ||
            RevealStarMapSettingsManager.Current.StarMapAction != StarMapAction.PermanentSurvey)
        {
            RevealSessionCoordinator.ToggleStarMap();
            return;
        }

        SurveyCurrentMap();
    }

    private static void SurveyCurrentMap()
    {
        ClusterGrid grid = ClusterGrid.Instance;
        var fog = RevealSnapshotStore.GetFog();
        if (grid == null || fog == null) return;

        var cells = new List<AxialI>();
        foreach (AxialI cell in grid.cellContents.Keys)
            if (grid.IsValidCell(cell)) cells.Add(cell);

        var surveyed = RevealSnapshotStore.PrepareSurvey(fog, cells, out int changed);
        if (changed == 0)
        {
            RevealSessionCoordinator.Notify("status.survey.empty");
            return;
        }

        // Permanent behavior is explicitly selected in the global Mod Options, never on load.
        try
        {
            RevealSnapshotStore.CommitSurvey(fog, surveyed);
            RevealSessionCoordinator.Notify("status.survey.done", changed);
        }
        catch (Exception exception)
        {
            Debug.LogError($"{RevealStarMapMod.Tag} Survey applied; a refresh callback failed. {exception}");
            RevealSessionCoordinator.Warn("error.callback");
        }
        RevealSnapshotStore.RefreshClusterMap();
    }
}
