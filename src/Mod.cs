using HarmonyLib;
using KMod;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using UnityEngine;

namespace RevealStarMap;

public sealed class RevealStarMapMod : UserMod2
{
    internal const string ModId = "liyux.RevealStarMap";
    internal const string ModName = "Reveal StarMap";
    internal static string Tag => $"[{ModName}]";

    public override void OnLoad(Harmony harmony)
    {
        base.OnLoad(harmony);
        PUtil.InitLibrary();
        new POptions().RegisterOptions(this, typeof(RevealStarMapSettings));
        Debug.Log($"{Tag} Loaded.");
    }
}
