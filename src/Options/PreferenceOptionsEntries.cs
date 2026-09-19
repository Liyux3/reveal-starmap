namespace RevealStarMap.Options;

internal sealed class LanguageOptionsEntry : EnumDropdownOptionsEntry<UiLanguage>
{
    public LanguageOptionsEntry(string field)
        : base(field, RevealText.Get("setting." + field), RevealText.Get("hint." + field),
            RevealText.Get("category.preferences"), BuildEnumChoices()) { }
}

internal sealed class SurveyConfirmationOptionsEntry : EnumDropdownOptionsEntry<SurveyConfirmation>
{
    public SurveyConfirmationOptionsEntry(string field)
        : base(field, RevealText.Get("setting." + field), RevealText.Get("hint." + field),
            RevealText.Get("category.preferences"), BuildEnumChoices()) { }
}

internal sealed class FeedbackOptionsEntry : EnumDropdownOptionsEntry<ActionFeedback>
{
    public FeedbackOptionsEntry(string field)
        : base(field, RevealText.Get("setting." + field), RevealText.Get("hint." + field),
            RevealText.Get("category.preferences"), BuildEnumChoices()) { }
}
