using UnityEngine;

namespace RevealStarMap.Runtime;

internal sealed class RevealStarMapController : KMonoBehaviour
{
    private bool initialized;

    protected override void OnSpawn()
    {
        base.OnSpawn();
        Initialize();
    }

    protected override void OnCleanUp()
    {
        RevealStarMapRuntime.CloseDialog();
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

        if (SaveGame.Instance == null || !DlcManager.FeatureClusterSpaceEnabled())
        {
            return;
        }

        if (RevealStarMapRuntime.DialogOpen || !RevealInput.CanProcessHotkeys())
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

        if (RevealStarMapSettingsManager.HotkeysConflict)
        {
            RevealStarMapSettings settings = RevealStarMapSettingsManager.Current;
            RevealSessionCoordinator.Notify($"Hotkey conflict: {RevealInput.FormatHotkey(settings.Modifier, settings.FunctionKey)} is assigned to both actions. Canvas preview stays disabled until one binding is changed.");
        }
    }
}
