using RevealStarMap.State;

namespace RevealStarMap.Runtime;

internal static class RevealStarMapRuntime
{
    internal static void RevealEntireMap()
    {
        if (SaveGame.Instance == null)
        {
            return;
        }

        var settings = RevealStarMapSettingsManager.Current;
        if (settings.CaptureSnapshotBeforeReveal)
        {
            RevealSnapshotStore.Capture();
        }

        ClusterFogOfWarManager.Instance fog = SaveGame.Instance.GetSMI<ClusterFogOfWarManager.Instance>();
        fog.DEBUG_REVEAL_ENTIRE_MAP();
        ClusterMapScreen.Instance?.Trigger(1980521255);

        if (settings.ShowNotification)
        {
            string suffix = RevealSnapshotStore.HasSnapshot()
                ? "Snapshot captured for a later restore-capable release."
                : "No snapshot captured.";
            Messenger.Instance?.QueueMessage(new RevealStarMapMessage($"The entire current starmap is now revealed. {suffix}"));
        }
    }
}
