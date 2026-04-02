using UnityEngine;

namespace RevealStarMap.Runtime;

internal sealed class RevealStarMapController : KMonoBehaviour
{
    private void Update()
    {
        if (SaveGame.Instance == null || !DlcManager.FeatureClusterSpaceEnabled())
        {
            return;
        }

        RevealStarMapSettings settings = RevealStarMapSettingsManager.Current;

        if (RevealInput.HotkeyPressed(settings.Modifier, settings.FunctionKey))
        {
            RevealStarMapRuntime.TriggerStarmapHotkeyAction();
            return;
        }

        if (RevealInput.HotkeyPressed(settings.CanvasModifier, settings.CanvasFunctionKey))
        {
            GridRevealRuntime.TriggerCanvasHotkeyAction();
        }
    }
}
