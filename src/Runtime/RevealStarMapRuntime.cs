using System;
using System.Collections.Generic;

namespace RevealStarMap.Runtime;

internal static class RevealStarMapRuntime
{
    private static ConfirmDialogScreen? dialog;

    internal static bool DialogOpen => dialog != null;

    internal static void TriggerStarmapHotkeyAction()
    {
        if (SaveGame.Instance == null || Game.Instance == null || Game.Instance.IsLoading() || DialogOpen)
            return;

        // An already active preview can always be closed, even after changing the setting.
        if (RevealSessionCoordinator.IsStarMapPreviewActive ||
            RevealStarMapSettingsManager.Current.StarMapAction == StarMapAction.TemporaryPreview)
        {
            RevealSessionCoordinator.ToggleStarMap();
            return;
        }

        RequestSurvey();
    }

    internal static void CloseDialog()
    {
        if (dialog != null) dialog.Deactivate();
        dialog = null;
    }

    private static void RequestSurvey()
    {
        SaveGame owner = SaveGame.Instance;
        ClusterGrid grid = ClusterGrid.Instance;
        var fog = RevealSnapshotStore.GetFog();
        if (grid == null || fog == null || ScreenPrefabs.Instance == null || GameScreenManager.Instance == null)
            return;

        var cells = new List<AxialI>();
        foreach (AxialI cell in grid.cellContents.Keys)
            if (grid.IsValidCell(cell)) cells.Add(cell);

        RevealSnapshotStore.PrepareSurvey(fog, cells, out int count);
        if (count == 0)
        {
            ShowDialog(RevealText.AlreadySurveyed,
                () => { }, "OK");
            return;
        }

        ShowDialog(RevealText.ConfirmSurvey(count), () =>
            {
                if (owner == null || SaveGame.Instance != owner || ClusterGrid.Instance != grid ||
                    !ReferenceEquals(RevealSnapshotStore.GetFog(), fog) || Game.Instance == null ||
                    Game.Instance.IsLoading() ||
                    RevealStarMapSettingsManager.Current.StarMapAction != StarMapAction.PermanentSurvey)
                    return;

                // Rebase on current research progress; survey only the cells confirmed in the dialog.
                cells.RemoveAll(cell => !grid.IsValidCell(cell));
                var surveyed = RevealSnapshotStore.PrepareSurvey(fog, cells, out int changed);
                if (changed == 0) return;
                try
                {
                    RevealSnapshotStore.CommitSurvey(fog, surveyed);
                    RevealSessionCoordinator.Notify($"Permanently analyzed {changed} starmap cells. " +
                        "Rocket routes use the saved analysis; fuel, range and landing rules still apply.");
                }
                catch (Exception exception)
                {
                    Debug.LogError($"{RevealStarMapMod.Tag} Survey points were applied, but a reveal-event or notification callback failed. {exception}");
                    ShowDialog(RevealText.Pick(
                        "Analysis points were applied, but a mod's notification or refresh callback failed. Check Player.log before continuing; reload the earlier save to revert this operation.",
                        "分析进度已应用，但某个模组的通知或刷新回调发生错误。请先检查 Player.log；需要回退时请载入操作前的存档。"),
                        () => { }, "OK");
                }
                RevealSnapshotStore.RefreshClusterMap();
            }, RevealText.ConfirmLabel);
    }

    private static void ShowDialog(string message, System.Action onConfirm, string confirmText)
    {
        var created = (ConfirmDialogScreen)GameScreenManager.Instance.StartScreen(
            ScreenPrefabs.Instance.ConfirmDialogScreen.gameObject);
        dialog = created;
        created.onDeactivateCB = () => { if (dialog == created) dialog = null; };
        bool used = false;
        created.PopupConfirmDialog(message, () =>
        {
            if (used) return;
            used = true;
            onConfirm();
        }, () => { used = true; }, title_text: "Reveal StarMap", confirm_text: confirmText);
    }
}
