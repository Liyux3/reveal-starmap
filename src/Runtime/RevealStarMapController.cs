using RevealStarMap.State;
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

        if (!HotkeyPressed(RevealStarMapSettingsManager.Current))
        {
            return;
        }

        RevealStarMapRuntime.TriggerHotkeyAction();
    }

    private static bool HotkeyPressed(RevealStarMapSettings settings)
    {
        if (!ModifierDown(settings.Modifier))
        {
            return false;
        }

        return Input.GetKeyDown(ToKeyCode(settings.FunctionKey));
    }

    private static bool ModifierDown(RevealModifier modifier)
    {
        return modifier switch
        {
            RevealModifier.None => true,
            RevealModifier.Shift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
            RevealModifier.Control => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl),
            RevealModifier.Alt => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt),
            RevealModifier.Command => Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand),
            _ => false
        };
    }

    private static KeyCode ToKeyCode(RevealFunctionKey functionKey)
    {
        return functionKey switch
        {
            RevealFunctionKey.F1 => KeyCode.F1,
            RevealFunctionKey.F2 => KeyCode.F2,
            RevealFunctionKey.F3 => KeyCode.F3,
            RevealFunctionKey.F4 => KeyCode.F4,
            RevealFunctionKey.F5 => KeyCode.F5,
            RevealFunctionKey.F6 => KeyCode.F6,
            RevealFunctionKey.F7 => KeyCode.F7,
            RevealFunctionKey.F8 => KeyCode.F8,
            RevealFunctionKey.F9 => KeyCode.F9,
            RevealFunctionKey.F10 => KeyCode.F10,
            RevealFunctionKey.F11 => KeyCode.F11,
            RevealFunctionKey.F12 => KeyCode.F12,
            _ => KeyCode.F7
        };
    }
}
