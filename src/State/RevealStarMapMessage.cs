using System;
using KSerialization;

namespace RevealStarMap.State;

[SerializationConfig(MemberSerialization.OptIn)]
public sealed class RevealStarMapMessage : Message
{
    [Serialize]
    private readonly string body = string.Empty;

    [Serialize]
    private readonly string textKey = string.Empty;

    [Serialize]
    private readonly string[] arguments = Array.Empty<string>();

    public RevealStarMapMessage(string key, object[] values)
    {
        textKey = key;
        arguments = Array.ConvertAll(values, value => value?.ToString() ?? string.Empty);
    }

    public override string GetTitle()
    {
        return RevealStarMapMod.ModName;
    }

    public override string GetSound()
    {
        return string.Empty;
    }

    public override string GetMessageBody()
    {
        return string.IsNullOrEmpty(textKey) ? body : RevealText.Format(textKey, arguments ?? Array.Empty<string>());
    }

    public override string GetTooltip()
    {
        return GetMessageBody();
    }

    public override bool ShowDialog()
    {
        return false;
    }

    public override bool PlayNotificationSound()
    {
        return false;
    }
}
