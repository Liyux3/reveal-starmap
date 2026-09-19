using System;
using System.Collections.Generic;
using HarmonyLib;
using RevealStarMap.Runtime;

namespace RevealStarMap.Patches;

internal static class StarMapPreviewQueryScope
{
    [ThreadStatic]
    private static int depth;

    internal static bool Active => depth > 0 && RevealSessionCoordinator.IsStarMapPreviewActive;

    internal static void Enter()
    {
        depth++;
    }

    internal static void Exit()
    {
        if (depth > 0)
        {
            depth--;
        }
    }
}

[HarmonyPatch(typeof(ClusterMapScreen), nameof(ClusterMapScreen.GetRevealLevel))]
public static class ClusterMapScreen_GetRevealLevel_Patch
{
    public static void Postfix(ref ClusterRevealLevel __result)
    {
        if (RevealSessionCoordinator.IsStarMapPreviewActive)
        {
            __result = ClusterRevealLevel.Visible;
        }
    }
}

[HarmonyPatch(typeof(ClusterMapHex), nameof(ClusterMapHex.SetRevealed))]
public static class ClusterMapHex_SetRevealed_Patch
{
    public static void Prefix(ref ClusterRevealLevel level)
    {
        if (RevealSessionCoordinator.IsStarMapPreviewActive)
        {
            level = ClusterRevealLevel.Visible;
        }
    }
}

[HarmonyPatch(typeof(ClusterGrid), nameof(ClusterGrid.GetVisibleEntitiesAtCell))]
public static class ClusterGrid_GetVisibleEntitiesAtCell_Patch
{
    public static void Postfix(ClusterGrid __instance, AxialI cell, ref List<ClusterGridEntity> __result)
    {
        if (!StarMapPreviewQueryScope.Active || !__instance.cellContents.TryGetValue(cell, out List<ClusterGridEntity>? entities))
        {
            return;
        }

        var visible = new List<ClusterGridEntity>(entities.Count);
        foreach (ClusterGridEntity entity in entities)
        {
            if (entity != null && entity.IsVisible)
            {
                visible.Add(entity);
            }
        }

        __result = visible;
    }
}

[HarmonyPatch(typeof(ClusterGrid), nameof(ClusterGrid.GetVisibleEntityOfLayerAtCell))]
public static class ClusterGrid_GetVisibleEntityOfLayerAtCell_Patch
{
    public static void Postfix(ClusterGrid __instance, AxialI cell, EntityLayer entityLayer, ref ClusterGridEntity __result)
    {
        if (__result != null || !StarMapPreviewQueryScope.Active || !__instance.cellContents.TryGetValue(cell, out List<ClusterGridEntity>? entities))
        {
            return;
        }

        foreach (ClusterGridEntity entity in entities)
        {
            if (entity != null && entity.IsVisible && entity.Layer == entityLayer)
            {
                __result = entity;
                return;
            }
        }
    }
}

[HarmonyPatch(typeof(ClusterMapScreen), nameof(ClusterMapScreen.SelectHex))]
public static class ClusterMapScreen_SelectHex_PreviewScope_Patch
{
    public static void Prefix()
    {
        StarMapPreviewQueryScope.Enter();
    }

    public static Exception? Finalizer(Exception? __exception)
    {
        StarMapPreviewQueryScope.Exit();
        return __exception;
    }
}

[HarmonyPatch(typeof(ClusterMapScreen), "UpdateVis")]
public static class ClusterMapScreen_UpdateVis_PreviewScope_Patch
{
    public static void Prefix()
    {
        StarMapPreviewQueryScope.Enter();
    }

    public static Exception? Finalizer(Exception? __exception)
    {
        StarMapPreviewQueryScope.Exit();
        return __exception;
    }
}

[HarmonyPatch(typeof(ClusterMapSelectTool), "UpdateHoveredSelectables")]
public static class ClusterMapSelectTool_UpdateHoveredSelectables_PreviewScope_Patch
{
    public static void Prefix()
    {
        StarMapPreviewQueryScope.Enter();
    }

    public static Exception? Finalizer(Exception? __exception)
    {
        StarMapPreviewQueryScope.Exit();
        return __exception;
    }
}

[HarmonyPatch(typeof(ClusterMapHex), "TryGoTo")]
public static class ClusterMapHex_TryGoTo_PreviewScope_Patch
{
    public static void Prefix()
    {
        StarMapPreviewQueryScope.Enter();
    }

    public static Exception? Finalizer(Exception? __exception)
    {
        StarMapPreviewQueryScope.Exit();
        return __exception;
    }
}
