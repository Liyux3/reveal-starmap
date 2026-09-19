using System;
using System.Collections.Generic;
using KSerialization;

namespace RevealStarMap.State;

[SerializationConfig(MemberSerialization.OptIn)]
public sealed class RevealSnapshotState : KMonoBehaviour
{
    [Serialize]
    private int previewMode;

    [Serialize]
    private int previewWorldId = -1;

    [Serialize]
    private bool notificationShown;

    [Serialize]
    private bool permanentSurveyConfirmed;

    internal bool PermanentSurveyConfirmed => permanentSurveyConfirmed;

    internal void MarkPermanentSurveyConfirmed() => permanentSurveyConfirmed = true;

    [Serialize]
    private int cameraSafetyVersion;

    [Serialize]
    private bool hasSnapshot;

    [Serialize]
    private bool revealApplied;

    [Serialize]
    private List<int> snapshotQs = new List<int>();

    [Serialize]
    private List<int> snapshotRs = new List<int>();

    [Serialize]
    private List<float> snapshotPoints = new List<float>();

    [Serialize]
    private bool hasGridSnapshot;

    [Serialize]
    private bool gridRevealApplied;

    [Serialize]
    private int gridSnapshotKind;

    [Serialize]
    private int gridSnapshotWorldId = -1;

    [Serialize]
    private int gridSnapshotOffsetX;

    [Serialize]
    private int gridSnapshotOffsetY;

    [Serialize]
    private int gridSnapshotWidth;

    [Serialize]
    private int gridSnapshotHeight;

    [Serialize]
    private byte[] gridVisibleSnapshot = Array.Empty<byte>();

    [Serialize]
    private byte[] gridSpawnableSnapshot = Array.Empty<byte>();

    internal bool HasSnapshot => hasSnapshot && snapshotQs.Count == snapshotRs.Count && snapshotQs.Count == snapshotPoints.Count;

    internal RevealPreviewMode PreviewMode => (RevealPreviewMode)previewMode;

    internal int PreviewWorldId => previewWorldId;

    internal int CameraSafetyVersion => cameraSafetyVersion;

    internal bool RevealApplied => revealApplied;

    internal bool HasGridSnapshot => hasGridSnapshot &&
        gridSnapshotWidth > 0 &&
        gridSnapshotHeight > 0 &&
        gridVisibleSnapshot.Length == gridSnapshotWidth * gridSnapshotHeight &&
        gridSpawnableSnapshot.Length == gridVisibleSnapshot.Length;

    internal bool GridRevealApplied => gridRevealApplied;

    internal GridRevealSnapshotKind GridSnapshotKind => (GridRevealSnapshotKind)gridSnapshotKind;

    internal int GridSnapshotWorldId => gridSnapshotWorldId;

    internal int GridSnapshotOffsetX => gridSnapshotOffsetX;

    internal int GridSnapshotOffsetY => gridSnapshotOffsetY;

    internal int GridSnapshotWidth => gridSnapshotWidth;

    internal int GridSnapshotHeight => gridSnapshotHeight;

    internal byte[] GridVisibleSnapshot => gridVisibleSnapshot;

    internal byte[] GridSpawnableSnapshot => gridSpawnableSnapshot;

    internal Dictionary<AxialI, float> RestoreSnapshot()
    {
        var restored = new Dictionary<AxialI, float>(snapshotQs.Count);
        for (int i = 0; i < snapshotQs.Count; i++)
        {
            restored[new AxialI(snapshotQs[i], snapshotRs[i])] = snapshotPoints[i];
        }

        return restored;
    }

    internal void ClearGridSnapshot()
    {
        hasGridSnapshot = false;
        gridRevealApplied = false;
        gridSnapshotKind = (int)GridRevealSnapshotKind.None;
        gridSnapshotWorldId = -1;
        gridSnapshotOffsetX = 0;
        gridSnapshotOffsetY = 0;
        gridSnapshotWidth = 0;
        gridSnapshotHeight = 0;
        gridVisibleSnapshot = Array.Empty<byte>();
        gridSpawnableSnapshot = Array.Empty<byte>();
    }

    internal void ClearSnapshot()
    {
        hasSnapshot = false;
        revealApplied = false;
        snapshotQs.Clear();
        snapshotRs.Clear();
        snapshotPoints.Clear();
    }

    internal void SetPreview(RevealPreviewMode mode, int worldId = -1)
    {
        previewMode = (int)mode;
        previewWorldId = mode == RevealPreviewMode.World ? worldId : -1;
    }

    internal bool TryMarkNotificationShown()
    {
        if (notificationShown)
        {
            return false;
        }

        notificationShown = true;
        return true;
    }

    internal void MarkCameraSafetyVersion(int version)
    {
        cameraSafetyVersion = version;
    }
}
