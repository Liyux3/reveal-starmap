using System;
using System.Collections.Generic;
using RevealStarMap.State;

namespace RevealStarMap.Runtime;

internal static class RevealStarMapRuntime
{
    private static ConfirmDialogScreen? dialog;

    internal static bool DialogOpen => dialog != null;

    internal static void TriggerStarmapHotkeyAction()
    {
        if (SaveGame.Instance == null || Game.Instance == null || Game.Instance.IsLoading() || DialogOpen)
            return;

        // An active preview always closes first, including after a preference change.
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
        var state = owner.GetComponent<RevealSnapshotState>();
        var fog = RevealSnapshotStore.GetFog();
        if (grid == null || fog == null || state == null ||
            ScreenPrefabs.Instance == null || GameScreenManager.Instance == null) return;

        var cells = new List<AxialI>();
        foreach (AxialI cell in grid.cellContents.Keys)
            if (grid.IsValidCell(cell)) cells.Add(cell);

        RevealSnapshotStore.PrepareSurvey(fog, cells, out int count);
        if (count == 0)
        {
            RevealSessionCoordinator.Notify("status.survey.empty");
            return;
        }

        void PerformSurvey()
        {
            if (owner == null || SaveGame.Instance != owner || ClusterGrid.Instance != grid ||
                !ReferenceEquals(RevealSnapshotStore.GetFog(), fog) || Game.Instance == null ||
                Game.Instance.IsLoading() ||
                RevealStarMapSettingsManager.Current.StarMapAction != StarMapAction.PermanentSurvey) return;

            // Keep consent scoped to the offered cells and rebase on current research progress.
            cells.RemoveAll(cell => !grid.IsValidCell(cell));
            var surveyed = RevealSnapshotStore.PrepareSurvey(fog, cells, out int changed);
            if (changed == 0) return;
            try
            {
                RevealSnapshotStore.CommitSurvey(fog, surveyed);
                state.MarkPermanentSurveyConfirmed();
                RevealSessionCoordinator.Notify("status.survey.done", changed);
            }
            catch (Exception exception)
            {
                Debug.LogError($"{RevealStarMapMod.Tag} Survey applied; a refresh callback failed. {exception}");
                ShowDialog(RevealText.Get("error.callback"), () => { },
                    RevealText.Get("error.title"), RevealText.Get("button.ok"), false);
            }
            RevealSnapshotStore.RefreshClusterMap();
        }

        var settings = RevealStarMapSettingsManager.Current;
        if (!settings.RequiresSurveyConfirmation(state.PermanentSurveyConfirmed))
        {
            PerformSurvey();
            return;
        }

        string message = RevealText.Format("survey.body", count);
        if (settings.SurveyConfirmation == SurveyConfirmation.OncePerSave)
            message += "\n\n" + RevealText.Get("survey.consent");
        ShowDialog(message, PerformSurvey, RevealText.Get("survey.title"), RevealText.Get("survey.confirm"));
    }

    private static void ShowDialog(string message, System.Action onConfirm, string title,
        string confirmText, bool cancellable = true)
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
        }, cancellable ? () => { used = true; } : (System.Action?)null,
            title_text: title, confirm_text: confirmText, cancel_text: RevealText.Get("button.cancel"));
    }
}
