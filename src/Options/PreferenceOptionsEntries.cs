namespace RevealStarMap.Options;

internal sealed class LanguageOptionsEntry : EnumDropdownOptionsEntry<UiLanguage>
{
    public LanguageOptionsEntry(string field)
        : base(field, RevealText.Get("setting." + field), RevealText.Get("hint." + field),
            RevealText.Get("category.preferences"), BuildEnumChoices()) { }
}

internal sealed class FeedbackOptionsEntry : EnumDropdownOptionsEntry<ActionFeedback>
{
    public FeedbackOptionsEntry(string field)
        : base(field, RevealText.Get("setting." + field), RevealText.Get("hint." + field),
            RevealText.Get("category.preferences"), new[]
            {
                (RevealText.Get("choice.Off"), string.Empty, ActionFeedback.Off),
                (RevealText.Get("choice.EveryAction"), string.Empty, ActionFeedback.EveryAction)
            }) { }
}
