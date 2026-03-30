using KSerialization;

namespace RevealStarMap.State;

[SerializationConfig(MemberSerialization.OptIn)]
public sealed class RevealStarMapMessage : Message
{
    [Serialize]
    private readonly string body;

    public RevealStarMapMessage(string body)
    {
        this.body = body;
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
        return body;
    }

    public override string GetTooltip()
    {
        return body;
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
