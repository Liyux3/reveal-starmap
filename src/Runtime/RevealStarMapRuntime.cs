using RevealStarMap.State;

namespace RevealStarMap.Runtime;

internal static class RevealStarMapRuntime
{
    internal static void TriggerStarmapHotkeyAction()
    {
        if (SaveGame.Instance == null)
        {
            return;
        }

        if (RevealSnapshotStore.ShouldRestore())
        {
            RestorePreviousReveal();
            return;
        }

        RevealEntireMap();
    }

    private static void RevealEntireMap()
    {
        ClusterFogOfWarManager.Instance? fog = SaveGame.Instance.GetSMI<ClusterFogOfWarManager.Instance>();
        if (fog == null)
        {
            return;
        }

        bool snapshotCaptured = RevealSnapshotStore.Capture();
        fog.DEBUG_REVEAL_ENTIRE_MAP();
        RevealSnapshotStore.MarkRevealApplied();
        RevealSnapshotStore.RefreshClusterMap();

        if (!RevealStarMapSettingsManager.Current.ShowNotification)
        {
            return;
        }

        string body = snapshotCaptured
            ? $"The entire current starmap is now revealed. Press {RevealInput.FormatHotkey(RevealStarMapSettingsManager.Current.Modifier, RevealStarMapSettingsManager.Current.FunctionKey)} again to restore the previous fog of war."
            : "The entire current starmap is now revealed. Snapshot capture failed, so this reveal cannot be restored later.";
        Messenger.Instance?.QueueMessage(new RevealStarMapMessage(body));
    }

    private static void RestorePreviousReveal()
    {
        bool restored = RevealSnapshotStore.Restore();
        if (!RevealStarMapSettingsManager.Current.ShowNotification)
        {
            return;
        }

        string body = restored
            ? "The previous fog-of-war state has been restored. Any sectors genuinely discovered while full reveal was active stay visible."
            : "No restorable fog-of-war snapshot was available.";
        Messenger.Instance?.QueueMessage(new RevealStarMapMessage(body));
    }
}
