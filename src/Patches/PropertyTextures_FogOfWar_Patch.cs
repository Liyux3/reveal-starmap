using HarmonyLib;
using RevealStarMap.Runtime;
using RevealStarMap.State;

namespace RevealStarMap.Patches;

[HarmonyPatch(typeof(PropertyTextures), "UpdateFogOfWar")]
public static class PropertyTextures_FogOfWar_Patch
{
    public static bool Prefix(TextureRegion region, int x0, int y0, int x1, int y1)
    {
        RevealPreviewMode mode = RevealSessionCoordinator.ActiveMode;
        if (mode != RevealPreviewMode.World && mode != RevealPreviewMode.Canvas)
        {
            return true;
        }

        int previewWorldId = RevealSessionCoordinator.ActiveWorldId;
        byte[] visible = Grid.Visible;
        int activeWorldTop = Grid.HeightInCells;
        if (ClusterManager.Instance != null && ClusterManager.Instance.activeWorld != null)
        {
            WorldContainer activeWorld = ClusterManager.Instance.activeWorld;
            activeWorldTop = activeWorld.WorldSize.y + activeWorld.WorldOffset.y - 1;
        }

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                int cell = Grid.XYToCell(x, y);
                bool revealCell = mode == RevealPreviewMode.Canvas ||
                    (Grid.IsValidCell(cell) && Grid.WorldIdx[cell] == previewWorldId);
                region.SetBytes(x, y, revealCell ? byte.MaxValue : GetVanillaVisibility(cell, x, activeWorldTop, visible));
            }
        }

        return false;
    }

    private static byte GetVanillaVisibility(int cell, int x, int activeWorldTop, byte[] visible)
    {
        if (Grid.IsActiveWorld(cell))
        {
            return visible[cell];
        }

        int activeWorldCell = Grid.XYToCell(x, activeWorldTop);
        return Grid.IsValidCell(activeWorldCell) ? visible[activeWorldCell] : (byte)0;
    }
}
