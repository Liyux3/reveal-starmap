using System;
using System.Collections.Generic;

namespace RevealStarMap.Options;

internal sealed class RevealFunctionKeyOptionsEntry : EnumDropdownOptionsEntry<RevealFunctionKey>
{
    public RevealFunctionKeyOptionsEntry(string field)
        : base(field, RevealText.Get("setting." + field), RevealText.Get("hint." + field),
            RevealText.Get(field == "CanvasFunctionKey" ? "category.canvas" : "category.map"), BuildChoices()) { }

    private static IEnumerable<(string Title, string Tooltip, RevealFunctionKey Value)> BuildChoices()
    {
        foreach (RevealFunctionKey key in Enum.GetValues(typeof(RevealFunctionKey)))
            yield return (key.ToString(), string.Empty, key);
    }
}
