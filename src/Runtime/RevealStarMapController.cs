using UnityEngine;

namespace RevealStarMap.Runtime;

internal sealed class RevealStarMapController : KMonoBehaviour
{
    private bool initialized;
    private int settingsRevision = -1;
    private string? reportedConflict;
    private string? reportedExportConflict;

    protected override void OnSpawn()
    {
        base.OnSpawn();
        Initialize();
    }

    protected override void OnCleanUp()
    {
        RevealSessionCoordinator.CleanupSession();
        base.OnCleanUp();
    }

    private void Update()
    {
        if (!initialized)
        {
            Initialize();
        }

        if (SaveGame.Instance == null || Game.Instance == null || Game.Instance.IsLoading() ||
            !DlcManager.FeatureClusterSpaceEnabled()) return;

        RevealSessionCoordinator.TryCompletePendingCameraSafetyMigration();

        if (settingsRevision != RevealStarMapSettingsManager.Revision)
        {
            settingsRevision = RevealStarMapSettingsManager.Revision;
            string? conflict = RevealStarMapSettingsManager.HotkeysConflict
                ? RevealInput.FormatHotkey(RevealStarMapSettingsManager.Current.Modifier, RevealStarMapSettingsManager.Current.FunctionKey)
                : null;
            if (conflict != null && conflict != reportedConflict)
                RevealSessionCoordinator.Warn("error.conflict", conflict);
            reportedConflict = conflict;
            string? exportConflict = RevealStarMapSettingsManager.ExportHotkeyConflict
                ? RevealInput.FormatHotkey(RevealStarMapSettingsManager.Current.ExportModifier, RevealStarMapSettingsManager.Current.ExportFunctionKey)
                : null;
            if (exportConflict != null && exportConflict != reportedExportConflict)
                RevealSessionCoordinator.Warn("error.export.conflict", exportConflict);
            reportedExportConflict = exportConflict;
        }

        if (!RevealInput.CanProcessHotkeys())
        {
            return;
        }

        RevealStarMapSettings settings = RevealStarMapSettingsManager.Current;

        if (RevealInput.HotkeyPressed(settings.Modifier, settings.FunctionKey))
        {
            RevealStarMapRuntime.TriggerStarmapHotkeyAction();
            return;
        }

        if (!RevealStarMapSettingsManager.HotkeysConflict &&
            RevealInput.HotkeyPressed(settings.CanvasModifier, settings.CanvasFunctionKey))
        {
            GridRevealRuntime.TriggerCanvasHotkeyAction();
            return;
        }

        if (!RevealStarMapSettingsManager.ExportHotkeyConflict &&
            RevealInput.HotkeyPressed(settings.ExportModifier, settings.ExportFunctionKey))
        {
            MapExport.Trigger(this);
        }
    }

    private void Initialize()
    {
        if (initialized || SaveGame.Instance == null)
        {
            return;
        }

        initialized = true;
        RevealStarMapSettingsManager.Reload();
        RevealSessionCoordinator.InitializeAndMigrate();
    }
}
