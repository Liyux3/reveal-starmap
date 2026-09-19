using System;
using System.Collections.Generic;

namespace RevealStarMap.Runtime;

internal static class StarMapSurvey
{
    // Build off to the side: cancellation or a failed preflight leaves the live save untouched.
    internal static Dictionary<AxialI, float> Prepare(Dictionary<AxialI, float> current,
        IEnumerable<AxialI> cells, float pointsToReveal, out int changed)
    {
        if (float.IsNaN(pointsToReveal) || float.IsInfinity(pointsToReveal) || pointsToReveal <= 0f)
            throw new ArgumentOutOfRangeException(nameof(pointsToReveal));

        var result = new Dictionary<AxialI, float>(current);
        changed = 0;
        foreach (AxialI cell in cells)
        {
            if (!result.TryGetValue(cell, out float points) || !(points >= pointsToReveal))
            {
                result[cell] = pointsToReveal;
                changed++;
            }
        }
        return result;
    }
}
