using Newtonsoft.Json;
using PeterHan.PLib.Options;

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
    [Option("Hotkey Modifier", "Modifier key required before the reveal trigger key.", "Input")]
    [JsonProperty]
    public RevealModifier Modifier { get; set; } = RevealModifier.Shift;

    [Option("Hotkey Function Key", "Function key used to reveal the entire starmap.", "Input")]
    [JsonProperty]
    public RevealFunctionKey FunctionKey { get; set; } = RevealFunctionKey.F7;

    [Option("Show Notification", "Show an in-game message when the starmap reveal is triggered.", "Behavior")]
    [JsonProperty]
    public bool ShowNotification { get; set; } = true;

    [Option("Capture Snapshot For Future Restore", "Internally capture the current reveal state before forcing full reveal. This is groundwork for a later invert or restore release.", "Behavior")]
    [JsonProperty]
    public bool CaptureSnapshotBeforeReveal { get; set; } = true;
}

internal static class RevealStarMapSettingsManager
{
    internal static RevealStarMapSettings Current => POptions.ReadSettings<RevealStarMapSettings>() ?? new RevealStarMapSettings();
}
