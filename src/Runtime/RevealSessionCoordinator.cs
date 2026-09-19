using System.Threading;
using HarmonyLib;
using RevealStarMap.Patches;
using RevealStarMap.State;
using UnityEngine;

namespace RevealStarMap.Runtime;

internal static class RevealSessionCoordinator
{
    private const int CurrentCameraSafetyVersion = 2;

    private static readonly AccessTools.FieldRef<CameraController, float> MaxOrthographicSizeRef =
        AccessTools.FieldRefAccess<CameraController, float>("maxOrthographicSize");

    private static int activeMode;
    private static int activeWorldId = -1;
    private static bool pendingCameraSafetyMigration;
    private static bool pendingForcedCameraReset;
    private static bool cameraSnapshotValid;
    private static Vector3 cameraSnapshotPosition;
    private static float cameraSnapshotOrthographicSize;
    private static float cameraSnapshotMaxOrthographicSize;
    private static int cameraSnapshotWorldId = -1;

    internal static RevealPreviewMode ActiveMode => (RevealPreviewMode)Volatile.Read(ref activeMode);

    internal static int ActiveWorldId => Volatile.Read(ref activeWorldId);

    internal static bool IsStarMapPreviewActive => ActiveMode == RevealPreviewMode.StarMap;

    internal static bool IsCanvasPreviewActive => ActiveMode == RevealPreviewMode.Canvas;

    internal static bool IsWorldPreviewActive(int worldId)
    {
        return ActiveMode == RevealPreviewMode.World && ActiveWorldId == worldId;
    }

    internal static void InitializeAndMigrate()
    {
        ResetRuntimeState();

        RevealSnapshotState? state = GetState();
        if (state == null)
        {
            return;
        }

        RevealPreviewMode persistedMode = state.PreviewMode;
        bool interruptedGridPreview = persistedMode == RevealPreviewMode.World || persistedMode == RevealPreviewMode.Canvas;
        RevealPreviewMode migratedMode = persistedMode == RevealPreviewMode.StarMap
            ? RevealPreviewMode.StarMap
            : RevealPreviewMode.None;
        int migratedWorldId = -1;

        if (RevealSnapshotStore.ShouldRestore() && RevealSnapshotStore.Restore())
        {
            migratedMode = RevealPreviewMode.StarMap;
            migratedWorldId = -1;
        }

        if (GridRevealSnapshotStore.HasActiveSnapshot())
        {
            GridRevealSnapshotKind legacyKind = GridRevealSnapshotStore.GetSnapshotKind();
            int legacyWorldId = GridRevealSnapshotStore.GetSnapshotWorldId();
            if (GridRevealSnapshotStore.Restore())
            {
                interruptedGridPreview = true;
                Debug.Log($"{RevealStarMapMod.Tag} Restored legacy {legacyKind} reveal data for world {legacyWorldId}; the old preview ended during migration.");
            }
        }

        state.SetPreview(migratedMode, migratedWorldId);
        SetRuntimeMode(migratedMode, migratedWorldId);

        if (interruptedGridPreview || state.CameraSafetyVersion < CurrentCameraSafetyVersion)
        {
            if (NormalizeCameraAfterInterruptedGridPreview(interruptedGridPreview))
            {
                state.MarkCameraSafetyVersion(CurrentCameraSafetyVersion);
            }
            else
            {
                pendingCameraSafetyMigration = true;
                pendingForcedCameraReset = interruptedGridPreview;
            }
        }

        if (migratedMode == RevealPreviewMode.StarMap)
        {
            RevealSnapshotStore.RefreshClusterMap();
        }
    }

    internal static void ToggleStarMap()
    {
        if (ActiveMode == RevealPreviewMode.StarMap)
        {
            EndPreview("Star map preview ended. Exploration progress was unchanged.");
            return;
        }

        BeginPreview(RevealPreviewMode.StarMap, -1);
        Notify($"Star map preview active. Press {RevealInput.FormatHotkey(RevealStarMapSettingsManager.Current.Modifier, RevealStarMapSettingsManager.Current.FunctionKey)} again to end it. " +
            "Rocket routes still require analysis. Choose Permanent Survey in Mod Options to analyze the starmap for navigation.");
    }

    internal static void ToggleCanvas()
    {
        if (!Grid.IsInitialized())
        {
            return;
        }

        if (ActiveMode == RevealPreviewMode.Canvas)
        {
            EndPreview("Canvas preview ended. Fog and simulation state were unchanged.");
            return;
        }

        BeginPreview(RevealPreviewMode.Canvas, -1);
        Notify($"Full live-canvas preview active. Press {RevealInput.FormatHotkey(RevealStarMapSettingsManager.Current.CanvasModifier, RevealStarMapSettingsManager.Current.CanvasFunctionKey)} again to end it.");
    }

    internal static void ToggleWorld(WorldContainer world)
    {
        if (world == null || !Grid.IsInitialized())
        {
            return;
        }

        string name = GridRevealRuntime.GetWorldName(world);
        if (IsWorldPreviewActive(world.id))
        {
            EndPreview($"World preview ended for {name}. Fog and discovery state were unchanged.");
            return;
        }

        BeginPreview(RevealPreviewMode.World, world.id);
        GridRevealRuntime.FocusWorld(world);
        Notify($"World preview active for {name}. Use the same button again to end it.");
    }

    internal static void ResetRuntimeState()
    {
        Volatile.Write(ref activeMode, (int)RevealPreviewMode.None);
        Volatile.Write(ref activeWorldId, -1);
        pendingCameraSafetyMigration = false;
        pendingForcedCameraReset = false;
        cameraSnapshotValid = false;
        cameraSnapshotWorldId = -1;
    }

    internal static void TryCompletePendingCameraSafetyMigration()
    {
        if (!pendingCameraSafetyMigration)
        {
            return;
        }

        RevealSnapshotState? state = GetState();
        if (state == null)
        {
            return;
        }

        if (NormalizeCameraAfterInterruptedGridPreview(pendingForcedCameraReset))
        {
            state.MarkCameraSafetyVersion(CurrentCameraSafetyVersion);
            pendingCameraSafetyMigration = false;
            pendingForcedCameraReset = false;
        }
    }

    internal static void CleanupSession()
    {
        RevealPreviewMode mode = ActiveMode;
        SetRuntimeMode(RevealPreviewMode.None, -1);
        CameraController? controller = CameraController.Instance;
        if (controller != null && (mode == RevealPreviewMode.World || mode == RevealPreviewMode.Canvas))
        {
            if (controller.ignoreClusterFX)
            {
                controller.ToggleClusterFX();
            }

            controller.FreeCameraEnabled = false;
            if (cameraSnapshotValid)
            {
                SetExactMaxOrthographicSize(controller, cameraSnapshotMaxOrthographicSize);
                if (ClusterManager.Instance != null && ClusterManager.Instance.activeWorldId == cameraSnapshotWorldId)
                {
                    controller.SnapTo(cameraSnapshotPosition, cameraSnapshotOrthographicSize);
                    ConstrainCameraToActiveWorld();
                }
            }
        }

        ResetRuntimeState();
    }

    internal static void EnforcePreviewZoomLimit(CameraController controller)
    {
        float limit;
        if (ActiveMode == RevealPreviewMode.Canvas && Grid.IsInitialized())
        {
            limit = GridRevealRuntime.ComputeOrthoSize(Grid.WidthInCells, Grid.HeightInCells);
        }
        else if (ActiveMode == RevealPreviewMode.World && ClusterManager.Instance != null)
        {
            WorldContainer? world = ClusterManager.Instance.GetWorld(ActiveWorldId);
            if (world == null)
            {
                return;
            }

            limit = GridRevealRuntime.ComputeOrthoSize(world.Width, world.Height);
        }
        else
        {
            return;
        }

        MaxOrthographicSizeRef(controller) = limit;
    }

    private static void BeginPreview(RevealPreviewMode mode, int worldId)
    {
        RevealPreviewMode previousMode = ActiveMode;
        bool previousWasGridPreview = previousMode == RevealPreviewMode.World || previousMode == RevealPreviewMode.Canvas;
        bool nextIsGridPreview = mode == RevealPreviewMode.World || mode == RevealPreviewMode.Canvas;

        EndPreviewInternal(restoreCamera: !(previousWasGridPreview && nextIsGridPreview));
        if (nextIsGridPreview && !previousWasGridPreview)
        {
            CaptureCameraSnapshot();
        }

        GetState()?.SetPreview(mode, worldId);
        SetRuntimeMode(mode, worldId);

        if (mode == RevealPreviewMode.Canvas)
        {
            FocusCanvas();
        }
        else if (mode == RevealPreviewMode.World)
        {
            PrepareWorldCamera(worldId);
        }
        else if (mode == RevealPreviewMode.StarMap)
        {
            RevealSnapshotStore.RefreshClusterMap();
        }
    }

    private static void EndPreview(string notification)
    {
        EndPreviewInternal(restoreCamera: true);
        Notify(notification);
    }

    private static void EndPreviewInternal(bool restoreCamera)
    {
        RevealPreviewMode previousMode = ActiveMode;
        SetRuntimeMode(RevealPreviewMode.None, -1);
        GetState()?.SetPreview(RevealPreviewMode.None);

        if (previousMode == RevealPreviewMode.StarMap)
        {
            RevealSnapshotStore.RefreshClusterMap();
        }

        if (restoreCamera && (previousMode == RevealPreviewMode.World || previousMode == RevealPreviewMode.Canvas))
        {
            RestoreCameraSnapshot();
        }

        CameraController.Instance?.VisibleArea.Update();
    }

    private static void SetRuntimeMode(RevealPreviewMode mode, int worldId)
    {
        Volatile.Write(ref activeWorldId, mode == RevealPreviewMode.World ? worldId : -1);
        Volatile.Write(ref activeMode, (int)mode);
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

        controller.FreeCameraEnabled = false;
        float zoom = GridRevealRuntime.ComputeOrthoSize(Grid.WidthInCells, Grid.HeightInCells);
        SetExactMaxOrthographicSize(controller, zoom);
        controller.SnapTo(new Vector3(Grid.WidthInCells / 2f, Grid.HeightInCells / 2f, -100f), zoom);
        CameraControllerVanillaBounds.Apply(controller);
    }

    private static void PrepareWorldCamera(int worldId)
    {
        CameraController? controller = CameraController.Instance;
        WorldContainer? world = ClusterManager.Instance?.GetWorld(worldId);
        if (controller == null || world == null)
        {
            return;
        }

        if (controller.ignoreClusterFX)
        {
            controller.ToggleClusterFX();
        }

        controller.FreeCameraEnabled = false;
        SetExactMaxOrthographicSize(controller, GridRevealRuntime.ComputeOrthoSize(world.Width, world.Height));
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
            if (controller.ignoreClusterFX)
            {
                controller.ToggleClusterFX();
            }

            controller.FreeCameraEnabled = false;
            SetExactMaxOrthographicSize(controller, cameraSnapshotMaxOrthographicSize);
            if (cameraSnapshotWorldId >= 0 &&
                ClusterManager.Instance != null &&
                ClusterManager.Instance.activeWorldId != cameraSnapshotWorldId)
            {
                controller.ActiveWorldStarWipe(
                    cameraSnapshotWorldId,
                    cameraSnapshotPosition,
                    cameraSnapshotOrthographicSize,
                    ConstrainCameraToActiveWorld);
            }
            else
            {
                controller.SnapTo(cameraSnapshotPosition, cameraSnapshotOrthographicSize);
                ConstrainCameraToActiveWorld();
            }
        }

        cameraSnapshotValid = false;
        cameraSnapshotWorldId = -1;
    }

    private static RevealSnapshotState? GetState()
    {
        return SaveGame.Instance == null ? null : SaveGame.Instance.GetComponent<RevealSnapshotState>();
    }

    internal static void Notify(string body)
    {
        Debug.Log($"{RevealStarMapMod.Tag} {body}");
        RevealSnapshotState? state = GetState();
        if (RevealStarMapSettingsManager.Current.ShowNotification && state != null && state.TryMarkNotificationShown())
        {
            Messenger.Instance?.QueueMessage(new RevealStarMapMessage(body));
        }
    }

    private static void ConstrainCameraToActiveWorld()
    {
        CameraController? controller = CameraController.Instance;
        if (controller != null)
        {
            controller.FreeCameraEnabled = false;
            CameraControllerVanillaBounds.Apply(controller);
        }
    }

    private static void SetExactMaxOrthographicSize(CameraController controller, float size)
    {
        controller.SetMaxOrthographicSize(size);
        MaxOrthographicSizeRef(controller) = size;
    }

    private static bool NormalizeCameraAfterInterruptedGridPreview(bool forceResetZoom)
    {
        CameraController? controller = CameraController.Instance;
        if (controller == null || Camera.main == null ||
            Game.Instance == null || Game.Instance.IsLoading() ||
            ClusterManager.Instance == null || ClusterManager.Instance.activeWorld == null)
        {
            return false;
        }

        bool hadIgnoredClusterFx = controller.ignoreClusterFX;
        bool hadFreeCamera = controller.FreeCameraEnabled;
        float maxZoom = MaxOrthographicSizeRef(controller);
        bool hadCanvasZoom = maxZoom > 250f || controller.targetOrthographicSize > 250f;

        if (hadIgnoredClusterFx)
        {
            controller.ToggleClusterFX();
        }

        controller.FreeCameraEnabled = false;
        float restoredMaxZoom = forceResetZoom || hadIgnoredClusterFx || hadFreeCamera || hadCanvasZoom ? 20f : maxZoom;
        controller.SetMaxOrthographicSize(restoredMaxZoom);
        float restoredZoom = Mathf.Min(controller.targetOrthographicSize, restoredMaxZoom);
        controller.SnapTo(controller.transform.GetPosition(), restoredZoom);
        ConstrainCameraToActiveWorld();
        Debug.Log($"{RevealStarMapMod.Tag} Camera bounds normalized after preview migration.");
        return true;
    }
}
