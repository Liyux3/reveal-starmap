using HarmonyLib;
using RevealStarMap.State;
using UnityEngine;

namespace RevealStarMap.Runtime;

internal static class GridRevealRuntime
{
    private static readonly AccessTools.FieldRef<CameraController, float> MaxOrthographicSizeRef =
        AccessTools.FieldRefAccess<CameraController, float>("maxOrthographicSize");

    private static bool cameraSnapshotValid;
    private static Vector3 cameraSnapshotPosition;
    private static float cameraSnapshotOrthographicSize;
    private static float cameraSnapshotMaxOrthographicSize;
    private static bool cameraSnapshotFreeCamera;
    private static bool cameraSnapshotIgnoreClusterFX;
    private static int cameraSnapshotWorldId = -1;

    internal static void TriggerCanvasHotkeyAction()
    {
        if (!Grid.IsInitialized())
        {
            return;
        }

        if (GridRevealSnapshotStore.Matches(GridRevealSnapshotKind.Canvas, -1))
        {
            bool restored = RestoreCurrentGridReveal();
            Notify(restored
                ? "The previous live simulation canvas fog has been restored."
                : "No restorable live simulation canvas snapshot was available.");
            return;
        }

        PrepareForNewReveal();
        CaptureCameraSnapshot();

        bool snapshotCaptured = GridRevealSnapshotStore.CaptureRect(
            GridRevealSnapshotKind.Canvas,
            -1,
            0,
            0,
            Grid.WidthInCells,
            Grid.HeightInCells);

        if (!snapshotCaptured)
        {
            Notify("Full canvas reveal could not capture a restorable snapshot.");
            return;
        }

        RevealRect(0, 0, Grid.WidthInCells, Grid.HeightInCells, null);
        GridRevealSnapshotStore.MarkApplied();
        FocusCanvas();

        RevealStarMapSettings settings = RevealStarMapSettingsManager.Current;
        Notify($"The full live simulation canvas is now revealed. Press {RevealInput.FormatHotkey(settings.CanvasModifier, settings.CanvasFunctionKey)} again to restore it.");
    }

    internal static void TriggerWorldRevealAction(WorldContainer world)
    {
        if (world == null || !Grid.IsInitialized())
        {
            return;
        }

        if (GridRevealSnapshotStore.Matches(GridRevealSnapshotKind.World, world.id))
        {
            bool restored = RestoreCurrentGridReveal();
            Notify(restored
                ? $"Restored the previous fog for {GetWorldName(world)}."
                : $"No restorable fog snapshot was available for {GetWorldName(world)}.");
            return;
        }

        PrepareForNewReveal();
        CaptureCameraSnapshot();

        bool snapshotCaptured = GridRevealSnapshotStore.CaptureRect(
            GridRevealSnapshotKind.World,
            world.id,
            world.WorldOffset.x,
            world.WorldOffset.y,
            world.Width,
            world.Height);

        if (!snapshotCaptured)
        {
            Notify($"Could not capture a restorable snapshot for {GetWorldName(world)}.");
            return;
        }

        RevealRect(world.WorldOffset.x, world.WorldOffset.y, world.Width, world.Height, world.id);
        GridRevealSnapshotStore.MarkApplied();
        FocusWorld(world);
        Notify($"{GetWorldName(world)} is now fully revealed for debug inspection. Use the same button again to restore its previous fog.");
    }

    internal static bool IsWorldRevealActive(WorldContainer world)
    {
        return world != null && GridRevealSnapshotStore.Matches(GridRevealSnapshotKind.World, world.id);
    }

    private static void PrepareForNewReveal()
    {
        if (!GridRevealSnapshotStore.HasActiveSnapshot())
        {
            return;
        }

        RestoreCurrentGridReveal();
    }

    private static bool RestoreCurrentGridReveal()
    {
        bool restored = GridRevealSnapshotStore.Restore();
        RestoreCameraSnapshot();
        return restored;
    }

    private static void RevealRect(int offsetX, int offsetY, int width, int height, int? worldId)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int cell = Grid.XYToCell(offsetX + x, offsetY + y);
                if (!Grid.IsValidCell(cell))
                {
                    continue;
                }

                if (worldId.HasValue && Grid.WorldIdx[cell] != worldId.Value)
                {
                    continue;
                }

                Grid.Reveal(cell, byte.MaxValue, forceReveal: true);
            }
        }

        CameraController.Instance?.VisibleArea.Update();
    }

    private static void FocusWorld(WorldContainer world)
    {
        CameraController? controller = CameraController.Instance;
        Vector3 focus = new Vector3(
            world.WorldOffset.x + world.Width / 2f,
            world.WorldOffset.y + world.Height / 2f,
            -100f);
        float zoom = ComputeOrthoSize(world.Width, world.Height);

        ManagementMenu.Instance?.CloseAll();

        if (controller == null)
        {
            ClusterManager.Instance.SetActiveWorld(world.id);
            return;
        }

        if (ClusterManager.Instance.activeWorldId != world.id)
        {
            controller.ActiveWorldStarWipe(world.id, focus, zoom);
            return;
        }

        controller.SnapTo(focus, zoom);
    }

    private static void FocusCanvas()
    {
        CameraController? controller = CameraController.Instance;
        if (controller == null)
        {
            return;
        }

        ManagementMenu.Instance?.CloseAll();

        if (!controller.ignoreClusterFX)
        {
            controller.ToggleClusterFX();
        }

        controller.EnableFreeCamera(true);

        float zoom = ComputeOrthoSize(Grid.WidthInCells, Grid.HeightInCells);
        float maxZoom = Mathf.Max(MaxOrthographicSizeRef(controller), zoom * 1.1f);
        controller.SetMaxOrthographicSize(maxZoom);
        controller.SnapTo(new Vector3(Grid.WidthInCells / 2f, Grid.HeightInCells / 2f, -100f), zoom);
    }

    private static float ComputeOrthoSize(int width, int height)
    {
        float aspect = Camera.main != null && Camera.main.aspect > 0f ? Camera.main.aspect : 16f / 9f;
        float vertical = height * 0.55f;
        float horizontal = width / (2f * aspect) * 1.1f;
        return Mathf.Max(10f, Mathf.Max(vertical, horizontal));
    }

    private static void CaptureCameraSnapshot()
    {
        CameraController? controller = CameraController.Instance;
        if (controller == null)
        {
            cameraSnapshotValid = false;
            return;
        }

        cameraSnapshotValid = true;
        cameraSnapshotPosition = controller.transform.GetPosition();
        cameraSnapshotOrthographicSize = controller.targetOrthographicSize;
        cameraSnapshotMaxOrthographicSize = MaxOrthographicSizeRef(controller);
        cameraSnapshotFreeCamera = controller.FreeCameraEnabled;
        cameraSnapshotIgnoreClusterFX = controller.ignoreClusterFX;
        cameraSnapshotWorldId = ClusterManager.Instance != null ? ClusterManager.Instance.activeWorldId : -1;
    }

    private static void RestoreCameraSnapshot()
    {
        if (!cameraSnapshotValid)
        {
            return;
        }

        CameraController? controller = CameraController.Instance;
        if (controller != null)
        {
            if (controller.ignoreClusterFX != cameraSnapshotIgnoreClusterFX)
            {
                controller.ToggleClusterFX();
            }

            controller.EnableFreeCamera(cameraSnapshotFreeCamera);
            controller.SetMaxOrthographicSize(cameraSnapshotMaxOrthographicSize);
            if (cameraSnapshotWorldId >= 0 &&
                ClusterManager.Instance != null &&
                ClusterManager.Instance.activeWorldId != cameraSnapshotWorldId)
            {
                controller.ActiveWorldStarWipe(cameraSnapshotWorldId, cameraSnapshotPosition, cameraSnapshotOrthographicSize);
            }
            else
            {
                controller.SnapTo(cameraSnapshotPosition, cameraSnapshotOrthographicSize);
            }
        }

        cameraSnapshotValid = false;
        cameraSnapshotWorldId = -1;
    }

    private static string GetWorldName(WorldContainer world)
    {
        ClusterGridEntity? entity = world.GetComponent<ClusterGridEntity>();
        return entity != null ? entity.GetProperName() : world.worldName;
    }

    private static void Notify(string body)
    {
        if (!RevealStarMapSettingsManager.Current.ShowNotification)
        {
            return;
        }

        Debug.Log($"{RevealStarMapMod.Tag} {body}");
        Messenger.Instance?.QueueMessage(new RevealStarMapMessage(body));
    }
}
