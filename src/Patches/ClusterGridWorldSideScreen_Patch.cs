using HarmonyLib;
using RevealStarMap.Runtime;
using UnityEngine;

namespace RevealStarMap.Patches;

[HarmonyPatch(typeof(ClusterGridWorldSideScreen))]
public static class ClusterGridWorldSideScreen_Patch
{
    private const string RevealButtonName = "RevealStarMapWorldButton";
    private static ClusterGridWorldSideScreen? activeScreen;

    private static readonly AccessTools.FieldRef<ClusterGridWorldSideScreen, AsteroidGridEntity> TargetEntityRef =
        AccessTools.FieldRefAccess<ClusterGridWorldSideScreen, AsteroidGridEntity>("targetEntity");

    static ClusterGridWorldSideScreen_Patch()
    {
        RevealSessionCoordinator.PreviewChanged += RefreshActiveButton;
        RevealStarMapSettingsManager.Changed += RefreshActiveButton;
    }

    private static void RefreshActiveButton()
    {
        if (activeScreen != null && activeScreen.viewButton != null) RefreshRevealButton(activeScreen);
    }

    [HarmonyPatch("OnSpawn")]
    [HarmonyPostfix]
    public static void OnSpawnPostfix(ClusterGridWorldSideScreen __instance)
    {
        activeScreen = __instance;
        RefreshActiveButton();
    }

    [HarmonyPatch(nameof(ClusterGridWorldSideScreen.SetTarget))]
    [HarmonyPostfix]
    public static void SetTargetPostfix(ClusterGridWorldSideScreen __instance)
    {
        activeScreen = __instance;
        RefreshActiveButton();
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
        cloneRect.SetSiblingIndex(templateRect.GetSiblingIndex() + 1);
        if (parent.GetComponent("VerticalLayoutGroup") == null && parent.GetComponent("GridLayoutGroup") == null)
        {
            cloneRect.anchoredPosition = templateRect.anchoredPosition + new Vector2(0f, -(templateRect.rect.height + 6f));
        }

        clone.onClick += delegate
        {
            AsteroidGridEntity? entity = TargetEntityRef(screen);
            WorldContainer? world = entity != null ? entity.GetComponent<WorldContainer>() : null;
            if (world != null)
            {
                GridRevealRuntime.TriggerWorldRevealAction(world);
                RefreshRevealButton(screen);
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
        SetButtonText(button, RevealText.Get(isRestore ? "world.end" : "world.preview"));

        if (tooltip != null)
        {
            tooltip.SetSimpleTooltip(RevealText.Get(!hasWorld ? "world.unavailable" :
                isRestore ? "world.end.hint" : "world.preview.hint"));
        }
    }

    private static void SetButtonText(KButton button, string text)
    {
        LocText? label = button.GetComponentInChildren<LocText>(includeInactive: true);
        if (label == null) return;
        label.key = string.Empty;
        label.text = text;
    }
}
