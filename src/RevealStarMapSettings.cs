using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using PeterHan.PLib;
using PeterHan.PLib.Options;
using RevealStarMap.Options;

namespace RevealStarMap;

public enum RevealModifier
{
    None,
    Shift,
    Control,
    Alt,
    Command
}

public enum RevealFunctionKey
{
    F1 = 1,
    F2,
    F3,
    F4,
    F5,
    F6,
    F7,
    F8,
    F9,
    F10,
    F11,
    F12
}

public enum StarMapAction
{
    TemporaryPreview,
    PermanentSurvey
}

public enum UiLanguage { Automatic, English, SimplifiedChinese, TraditionalChinese }
// Keep the existing numeric values so previous configs continue to deserialize correctly.
public enum ActionFeedback { EveryAction = 1, Off = 2 }

[JsonObject(MemberSerialization.OptIn)]
[ConfigFile("config.json", true, true)]
public sealed class RevealStarMapSettings : IOptions
{
    private ActionFeedback feedback = ActionFeedback.Off;

    [DynamicOption(typeof(StarMapActionOptionsEntry))]
    [JsonProperty]
    public StarMapAction StarMapAction { get; set; } = StarMapAction.TemporaryPreview;

    [DynamicOption(typeof(RevealModifierOptionsEntry))]
    [JsonProperty]
    public RevealModifier Modifier { get; set; } = RevealModifier.Shift;

    [DynamicOption(typeof(RevealFunctionKeyOptionsEntry))]
    [JsonProperty]
    public RevealFunctionKey FunctionKey { get; set; } = RevealFunctionKey.F7;

    [DynamicOption(typeof(RevealModifierOptionsEntry))]
    [JsonProperty]
    public RevealModifier CanvasModifier { get; set; } = RevealModifier.Shift;

    [DynamicOption(typeof(RevealFunctionKeyOptionsEntry))]
    [JsonProperty]
    public RevealFunctionKey CanvasFunctionKey { get; set; } = RevealFunctionKey.F8;

    [DynamicOption(typeof(LanguageOptionsEntry))]
    [JsonProperty]
    public UiLanguage Language { get; set; } = UiLanguage.Automatic;

    [DynamicOption(typeof(FeedbackOptionsEntry))]
    [JsonProperty]
    public ActionFeedback Feedback
    {
        get => feedback == ActionFeedback.EveryAction ? feedback : ActionFeedback.Off;
        set => feedback = value;
    }

    public IEnumerable<IOptionsEntry> CreateOptions() => Array.Empty<IOptionsEntry>();

    public void OnOptionsChanged() => RevealStarMapSettingsManager.Apply(this);

    internal void Normalize()
    {
        if (!Enum.IsDefined(typeof(StarMapAction), StarMapAction))
            StarMapAction = StarMapAction.TemporaryPreview;
        if (!Enum.IsDefined(typeof(UiLanguage), Language)) Language = UiLanguage.Automatic;
        if (!Enum.IsDefined(typeof(RevealModifier), Modifier)) Modifier = RevealModifier.Shift;
        if (!Enum.IsDefined(typeof(RevealModifier), CanvasModifier)) CanvasModifier = RevealModifier.Shift;
        if (!Enum.IsDefined(typeof(RevealFunctionKey), FunctionKey)) FunctionKey = RevealFunctionKey.F7;
        if (!Enum.IsDefined(typeof(RevealFunctionKey), CanvasFunctionKey)) CanvasFunctionKey = RevealFunctionKey.F8;
    }

}

internal static class RevealStarMapSettingsManager
{
    private static RevealStarMapSettings current = new RevealStarMapSettings();

    internal static int Revision { get; private set; }

    internal static event System.Action? Changed;

    internal static RevealStarMapSettings Current => current;

    internal static bool HotkeysConflict =>
        current.Modifier == current.CanvasModifier && current.FunctionKey == current.CanvasFunctionKey;

    internal static void Apply(RevealStarMapSettings settings)
    {
        settings.Normalize();
        current = settings;
        Revision++;
        Changed?.Invoke();
    }

    internal static void Reload()
    {
        try
        {
            Apply(POptions.ReadSettings<RevealStarMapSettings>() ?? new RevealStarMapSettings());
        }
        catch (Exception exception)
        {
            current = new RevealStarMapSettings();
            Debug.LogError($"{RevealStarMapMod.Tag} Could not read config.json; defaults are active for this session. {exception}");
        }
    }
}
