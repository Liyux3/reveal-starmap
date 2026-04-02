using System.Collections.Generic;

namespace RevealStarMap.Options;

internal sealed class RevealFunctionKeyOptionsEntry : EnumDropdownOptionsEntry<RevealFunctionKey>
{
    public RevealFunctionKeyOptionsEntry(string field)
        : base(
            field,
            field == "CanvasFunctionKey" ? "Canvas Hotkey Function Key" : "StarMap Hotkey Function Key",
            field == "CanvasFunctionKey"
                ? "Function key used to reveal the full live simulation canvas across all worlds."
                : "Function key used to reveal the entire starmap.",
            "Input",
            BuildChoices())
    {
    }

    private static IEnumerable<(string Title, string Tooltip, RevealFunctionKey Value)> BuildChoices()
    {
        return new (string, string, RevealFunctionKey)[]
        {
            ("F1", "Use F1.", RevealFunctionKey.F1),
            ("F2", "Use F2.", RevealFunctionKey.F2),
            ("F3", "Use F3.", RevealFunctionKey.F3),
            ("F4", "Use F4.", RevealFunctionKey.F4),
            ("F5", "Use F5.", RevealFunctionKey.F5),
            ("F6", "Use F6.", RevealFunctionKey.F6),
            ("F7", "Use F7.", RevealFunctionKey.F7),
            ("F8", "Use F8.", RevealFunctionKey.F8),
            ("F9", "Use F9.", RevealFunctionKey.F9),
            ("F10", "Use F10.", RevealFunctionKey.F10),
            ("F11", "Use F11.", RevealFunctionKey.F11),
            ("F12", "Use F12.", RevealFunctionKey.F12)
        };
    }
}
