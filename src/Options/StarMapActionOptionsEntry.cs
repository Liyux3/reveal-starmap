namespace RevealStarMap.Options;

internal sealed class StarMapActionOptionsEntry : EnumDropdownOptionsEntry<StarMapAction>
{
    public StarMapActionOptionsEntry(string field)
        : base(field, RevealText.Get("setting." + field), RevealText.Get("hint." + field),
            RevealText.Category("map"), BuildEnumChoices())
    {
    }
}
