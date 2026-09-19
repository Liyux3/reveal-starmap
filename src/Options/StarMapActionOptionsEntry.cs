namespace RevealStarMap.Options;

internal sealed class StarMapActionOptionsEntry : EnumDropdownOptionsEntry<StarMapAction>
{
    public StarMapActionOptionsEntry(string field)
        : base(field, RevealText.ActionTitle, RevealText.ActionTooltip,
            "Behavior", new[]
            {
                (RevealText.PreviewTitle, RevealText.PreviewTooltip, StarMapAction.TemporaryPreview),
                (RevealText.SurveyTitle, RevealText.SurveyTooltip, StarMapAction.PermanentSurvey)
            })
    {
    }
}
