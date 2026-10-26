using UnityEngine;

namespace RevealStarMap.Runtime;

internal static class GridRevealRuntime
{
    internal static void TriggerCanvasHotkeyAction()
    {
        RevealSessionCoordinator.ToggleCanvas();
    }

    internal static void TriggerWorldRevealAction(WorldContainer world)
    {
        RevealSessionCoordinator.ToggleWorld(world);
    }

    internal static bool IsWorldRevealActive(WorldContainer world)
    {
        return world != null && RevealSessionCoordinator.IsWorldPreviewActive(world.id);
    }

    internal static void FocusWorld(WorldContainer world)
    {
        CameraController? controller = CameraController.Instance;
        Vector3 focus = new Vector3(
            world.WorldOffset.x + world.Width / 2f,
            world.WorldOffset.y + world.Height / 2f,
            -100f);
        float zoom = ComputeOrthoSize(world.Width, world.Height);

        ManagementMenu.Instance?.CloseAll();
        // Match the stock Oversee Planetoid action: switch synchronously, then frame it.
        // No delayed star-wipe callback can outlive an ended or superseded preview.
        ClusterManager.Instance.SetActiveWorld(world.id);
        controller?.SnapTo(focus, zoom);
    }

    internal static float ComputeOrthoSize(int width, int height)
    {
        float aspect = Camera.main != null && Camera.main.aspect > 0f ? Camera.main.aspect : 16f / 9f;
        float vertical = height * 0.55f;
        float horizontal = width / (2f * aspect) * 1.1f;
        return Mathf.Max(10f, Mathf.Max(vertical, horizontal));
    }

    internal static string GetWorldName(WorldContainer world)
    {
        ClusterGridEntity? entity = world.GetComponent<ClusterGridEntity>();
        return entity != null ? entity.GetProperName() : world.worldName;
    }
}
