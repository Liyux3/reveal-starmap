using System.Collections.Generic;

namespace RevealStarMap.Options;

internal sealed class RevealModifierOptionsEntry : EnumDropdownOptionsEntry<RevealModifier>
{
    public RevealModifierOptionsEntry(string field)
        : base(
            field,
            field == "CanvasModifier" ? "Canvas Hotkey Modifier" : "StarMap Hotkey Modifier",
            field == "CanvasModifier"
                ? "Modifier key required before the full live-canvas preview trigger key."
                : "Modifier key for the selected Star Map Action. Use exactly this modifier.",
            "Input",
            BuildChoices())
    {
    }

    private static IEnumerable<(string Title, string Tooltip, RevealModifier Value)> BuildChoices()
    {
        return new (string, string, RevealModifier)[]
        {
            ("None", "No modifier key is required.", RevealModifier.None),
            ("Shift", "Use the Shift key as the modifier.", RevealModifier.Shift),
            ("Control", "Use the Control key as the modifier.", RevealModifier.Control),
            ("Alt", "Use the Alt or Option key as the modifier.", RevealModifier.Alt),
            ("Command", "Use the macOS Command key as the modifier.", RevealModifier.Command)
        };
    }
}
