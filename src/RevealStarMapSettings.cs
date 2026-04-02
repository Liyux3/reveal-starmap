using Newtonsoft.Json;
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

[JsonObject(MemberSerialization.OptIn)]
[ConfigFile("config.json", true, true)]
public sealed class RevealStarMapSettings
{
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

    [Option("Show Notification", "Show an in-game message when the starmap reveal is triggered.", "Behavior")]
    [JsonProperty]
    public bool ShowNotification { get; set; } = true;
}

internal static class RevealStarMapSettingsManager
{
    internal static RevealStarMapSettings Current => POptions.ReadSettings<RevealStarMapSettings>() ?? new RevealStarMapSettings();
}
