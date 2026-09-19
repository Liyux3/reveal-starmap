using HarmonyLib;
using RevealStarMap.Runtime;
using UnityEngine;

namespace RevealStarMap.Patches;

[HarmonyPatch(typeof(CameraController), "ConstrainToWorld")]
public static class CameraController_CanvasPreview_Patch
{
    public static bool Prefix()
    {
        return !RevealSessionCoordinator.IsCanvasPreviewActive;
    }
}

internal static class CameraControllerVanillaBounds
{
    internal static void Apply(CameraController controller)
    {
        if (Game.Instance == null || Game.Instance.IsLoading() || controller.FreeCameraEnabled ||
            Camera.main == null)
        {
            return;
        }

        Camera camera = Camera.main;
        Ray lowerRay = camera.ViewportPointToRay(Vector3.one * 0.33f);
        Ray upperRay = camera.ViewportPointToRay(Vector3.one - Vector3.one * 0.33f);
        float lowerDistance = Mathf.Abs(lowerRay.origin.z / lowerRay.direction.z);
        float upperDistance = Mathf.Abs(upperRay.origin.z / upperRay.direction.z);
        Vector3 lowerPoint = lowerRay.GetPoint(lowerDistance);
        Vector3 upperPoint = upperRay.GetPoint(upperDistance);
        Vector2 boundsMin;
        Vector2 boundsMax;
        Vector2 boundsSize;
        float upperBoundsY;

        if (RevealSessionCoordinator.IsCanvasPreviewActive)
        {
            if (!Grid.IsInitialized())
            {
                return;
            }

            boundsMin = Vector2.zero;
            boundsMax = new Vector2(Grid.WidthInMeters, Grid.HeightInMeters);
            boundsSize = boundsMax;
            upperBoundsY = boundsMax.y;
        }
        else
        {
            if (ClusterManager.Instance == null || ClusterManager.Instance.activeWorld == null)
            {
                return;
            }

            WorldContainer world = ClusterManager.Instance.activeWorld;
            boundsMin = world.minimumBounds * Grid.CellSizeInMeters;
            boundsMax = world.maximumBounds * Grid.CellSizeInMeters;
            boundsSize = new Vector2(world.Width, world.Height) * Grid.CellSizeInMeters;
            upperBoundsY = boundsMax.y * 1.1f;
        }

        Vector3 position = controller.transform.GetPosition();
        float viewportWidth = upperPoint.x - lowerPoint.x;
        float viewportHeight = upperPoint.y - lowerPoint.y;

        if (viewportWidth > boundsSize.x)
        {
            position.x = (boundsMin.x + boundsMax.x) * 0.5f;
        }
        else
        {
            float correction = Mathf.Max(0f, boundsMin.x - lowerPoint.x);
            correction -= Mathf.Max(0f, upperPoint.x + correction - boundsMax.x);
            position.x += correction;
        }

        if (viewportHeight > boundsSize.y)
        {
            position.y = (boundsMin.y + upperBoundsY) * 0.5f;
        }
        else
        {
            float correction = Mathf.Max(0f, boundsMin.y - lowerPoint.y);
            correction -= Mathf.Max(0f, upperPoint.y + correction - upperBoundsY);
            position.y += correction;
        }

        position.z = -100f;
        controller.transform.SetPosition(position);
    }
}

[HarmonyPatch(typeof(CameraController), "Update")]
public static class CameraController_Update_Bounds_Patch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    public static void Prefix(CameraController __instance)
    {
        RevealSessionCoordinator.EnforcePreviewZoomLimit(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    public static void Postfix(CameraController __instance)
    {
        CameraControllerVanillaBounds.Apply(__instance);
    }
}
