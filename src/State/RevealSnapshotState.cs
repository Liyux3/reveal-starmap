using System.Collections.Generic;
using KSerialization;

namespace RevealStarMap.State;

[SerializationConfig(MemberSerialization.OptIn)]
public sealed class RevealSnapshotState : KMonoBehaviour
{
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

    internal bool HasSnapshot => hasSnapshot && snapshotQs.Count == snapshotRs.Count && snapshotQs.Count == snapshotPoints.Count;

    internal bool RevealApplied => revealApplied;

    internal void Store(Dictionary<AxialI, float> revealPoints)
    {
        snapshotQs.Clear();
        snapshotRs.Clear();
        snapshotPoints.Clear();

        foreach (KeyValuePair<AxialI, float> revealPoint in revealPoints)
        {
            snapshotQs.Add(revealPoint.Key.Q);
            snapshotRs.Add(revealPoint.Key.R);
            snapshotPoints.Add(revealPoint.Value);
        }

        hasSnapshot = true;
        revealApplied = false;
    }

    internal Dictionary<AxialI, float> RestoreSnapshot()
    {
        var restored = new Dictionary<AxialI, float>(snapshotQs.Count);
        for (int i = 0; i < snapshotQs.Count; i++)
        {
            restored[new AxialI(snapshotQs[i], snapshotRs[i])] = snapshotPoints[i];
        }

        return restored;
    }

    internal void MarkRevealApplied()
    {
        revealApplied = true;
    }

    internal void ClearSnapshot()
    {
        hasSnapshot = false;
        revealApplied = false;
        snapshotQs.Clear();
        snapshotRs.Clear();
        snapshotPoints.Clear();
    }
}
