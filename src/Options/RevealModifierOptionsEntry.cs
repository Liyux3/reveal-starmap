namespace RevealStarMap.Options;

internal sealed class RevealModifierOptionsEntry : EnumDropdownOptionsEntry<RevealModifier>
{
    public RevealModifierOptionsEntry(string field)
        : base(field, RevealText.Get("setting." + field), RevealText.Get("hint." + field),
            RevealText.Category(field == "CanvasModifier" ? "canvas" : "map"), BuildEnumChoices()) { }
}
