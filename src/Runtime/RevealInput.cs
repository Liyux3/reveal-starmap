using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace RevealStarMap.Runtime;

internal static class RevealInput
{
    private static readonly AccessTools.FieldRef<KScreenManager, List<KScreen>> ScreensRef =
        AccessTools.FieldRefAccess<KScreenManager, List<KScreen>>("screenStack");

    internal static bool CanProcessHotkeys()
    {
        CameraController? camera = CameraController.Instance;
        if (!Application.isFocused || Game.Instance == null || Game.Instance.IsLoading() ||
            !Grid.IsInitialized() || camera == null || camera.activeFadeRoutine != null)
            return false;

        if (KScreenManager.Instance != null)
        {
            foreach (KScreen screen in ScreensRef(KScreenManager.Instance))
                if (screen != null && screen.IsScreenActive() && screen.IsModal()) return false;
        }

        GameObject? selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
        if (selected == null)
        {
            return true;
        }

        return selected.GetComponentInParent<KInputTextField>() == null &&
            selected.GetComponentInParent<TMPro.TMP_InputField>() == null;
    }

    internal static bool HotkeyPressed(RevealModifier modifier, RevealFunctionKey functionKey)
    {
        return ModifierDown(modifier) && Input.GetKeyDown(ToKeyCode(functionKey));
    }

    internal static string FormatHotkey(RevealModifier modifier, RevealFunctionKey functionKey)
    {
        string prefix = modifier switch
        {
            RevealModifier.None => string.Empty,
            RevealModifier.Shift => "Shift + ",
            RevealModifier.Control => "Ctrl + ",
            RevealModifier.Alt => "Alt + ",
            RevealModifier.Command => "Command + ",
            _ => string.Empty
        };

        return $"{prefix}{functionKey}";
    }

    private static bool ModifierDown(RevealModifier modifier)
    {
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        bool command = Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
        return modifier switch
        {
            RevealModifier.None => !shift && !control && !alt && !command,
            RevealModifier.Shift => shift && !control && !alt && !command,
            RevealModifier.Control => control && !shift && !alt && !command,
            RevealModifier.Alt => alt && !shift && !control && !command,
            RevealModifier.Command => command && !shift && !control && !alt,
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
