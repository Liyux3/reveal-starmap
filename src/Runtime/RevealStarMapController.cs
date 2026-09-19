using UnityEngine;

namespace RevealStarMap.Runtime;

internal sealed class RevealStarMapController : KMonoBehaviour
{
    private bool initialized;
    private int settingsRevision = -1;
    private string? reportedConflict;

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
        }

        if (SaveGame.Instance == null || !DlcManager.FeatureClusterSpaceEnabled())
        {
            return;
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
