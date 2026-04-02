using HarmonyLib;
using RevealStarMap.Runtime;
using UnityEngine;

namespace RevealStarMap.Patches;

[HarmonyPatch(typeof(ClusterGridWorldSideScreen))]
public static class ClusterGridWorldSideScreen_Patch
{
    private const string RevealButtonName = "RevealStarMapWorldButton";

    private static readonly AccessTools.FieldRef<ClusterGridWorldSideScreen, AsteroidGridEntity> TargetEntityRef =
        AccessTools.FieldRefAccess<ClusterGridWorldSideScreen, AsteroidGridEntity>("targetEntity");

    [HarmonyPatch("OnSpawn")]
    [HarmonyPostfix]
    public static void OnSpawnPostfix(ClusterGridWorldSideScreen __instance)
    {
        EnsureRevealButton(__instance);
    }

    [HarmonyPatch(nameof(ClusterGridWorldSideScreen.SetTarget))]
    [HarmonyPostfix]
    public static void SetTargetPostfix(ClusterGridWorldSideScreen __instance)
    {
        RefreshRevealButton(__instance);
    }

    private static KButton EnsureRevealButton(ClusterGridWorldSideScreen screen)
    {
        Transform parent = screen.viewButton.transform.parent;
        Transform? existing = parent.Find(RevealButtonName);
        if (existing != null)
        {
            return existing.GetComponent<KButton>();
        }

        KButton clone = Util.KInstantiateUI<KButton>(screen.viewButton.gameObject, parent.gameObject, force_active: true);
        clone.name = RevealButtonName;
        clone.ClearOnClick();

        RectTransform cloneRect = clone.rectTransform();
        RectTransform templateRect = screen.viewButton.rectTransform();
        cloneRect.anchorMin = templateRect.anchorMin;
        cloneRect.anchorMax = templateRect.anchorMax;
        cloneRect.pivot = templateRect.pivot;
        cloneRect.sizeDelta = templateRect.sizeDelta;
        cloneRect.anchoredPosition = templateRect.anchoredPosition + new Vector2(0f, -52f);

        clone.onClick += delegate
        {
            AsteroidGridEntity? entity = TargetEntityRef(screen);
            WorldContainer? world = entity != null ? entity.GetComponent<WorldContainer>() : null;
            if (world != null)
            {
                GridRevealRuntime.TriggerWorldRevealAction(world);
            }
        };

        return clone;
    }

    private static void RefreshRevealButton(ClusterGridWorldSideScreen screen)
    {
        KButton button = EnsureRevealButton(screen);
        AsteroidGridEntity? entity = TargetEntityRef(screen);
        WorldContainer? world = entity != null ? entity.GetComponent<WorldContainer>() : null;
        ToolTip? tooltip = button.GetComponent<ToolTip>();

        bool hasWorld = world != null;
        bool isRestore = false;
        if (world != null)
        {
            isRestore = GridRevealRuntime.IsWorldRevealActive(world);
        }

        button.isInteractable = hasWorld;
        SetButtonText(button, isRestore ? "Restore World Map" : "Reveal World Map");

        if (tooltip != null)
        {
            tooltip.SetSimpleTooltip(isRestore
                ? "Restore the previously saved fog-of-war state for this world."
                : "Fully reveal this world's map for debug inspection, then switch the camera there.");
        }
    }

    private static void SetButtonText(KButton button, string text)
    {
        Component[] components = button.GetComponentsInChildren<Component>(includeInactive: true);
        foreach (Component component in components)
        {
            if (component.GetType().Name != "LocText")
            {
                continue;
            }

            AccessTools.Property(component.GetType(), "text")?.SetValue(component, text, null);
            return;
        }
    }
}
