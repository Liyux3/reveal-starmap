namespace RevealStarMap;

internal static class RevealText
{
    internal static string Pick(string english, string chinese) =>
        Localization.GetLocale()?.Lang == Localization.Language.Chinese ? chinese : english;

    internal static string ActionTitle => Pick("Star Map Action", "星图快捷键行为");
    internal static string ActionTooltip => Pick(
        "Changing this option never modifies a save. Permanent Survey requires in-game confirmation and persists after the mod is removed.",
        "修改此选项不会更改存档。永久分析需要在游戏内确认，移除模组后仍然保留。");
    internal static string PreviewTitle => Pick("Temporary Preview", "临时预览");
    internal static string PreviewTooltip => Pick(
        "View the starmap, then press again to restore normal fog. Rocket routes still require real analysis.",
        "临时查看星图，再按一次恢复正常迷雾。火箭航行仍需完成真实分析。");
    internal static string SurveyTitle => Pick("Permanent Survey", "永久分析星图");
    internal static string SurveyTooltip => Pick(
        "Analyze the current starmap for navigation. Skips telescope work and its Data Bank rewards. Requires confirmation; has no undo.",
        "分析当前星图以解锁航路。跳过望远镜分析及其数据磁盘奖励。需要确认，无法通过快捷键撤销。");
    internal static string ConfirmLabel => Pick("Analyze Permanently", "确认永久分析");
    internal static string AlreadySurveyed => Pick(
        "The current starmap is already fully analyzed. Rocket fuel, range and landing requirements still apply. Newly generated cells can be surveyed with this hotkey later.",
        "当前星图已全部分析。火箭仍需满足燃料、射程和着陆条件。以后新增的星图格子可以再次通过此快捷键分析。");

    internal static string ConfirmSurvey(int count) => Pick(
        $"Permanently analyze {count} unexplored or partially analyzed starmap cells in this save?\n\n" +
        "This opens them for rocket navigation. Fuel, range and landing requirements still apply. Asteroid terrain and visit records are preserved.\n\n" +
        "Telescope analysis and its Data Bank rewards are skipped. Debug and sandbox flags stay unchanged, so this action itself does not disable Steam achievements. Exploration progression changes; other mods may handle achievements differently.\n\n" +
        "This is saved permanently and persists after removing the mod. Reload an earlier save to undo.",
        $"要永久分析当前存档中尚未完成分析的 {count} 个星图格子吗？\n\n" +
        "这些格子将可用于火箭导航，燃料、射程和着陆要求仍然生效。星球地形与实际访问记录保持原状。\n\n" +
        "将跳过望远镜的分析工作及其数据磁盘奖励。本操作保持调试和沙盒标记原状，本身不会禁用 Steam 成就；探索进程会改变，其他模组的成就规则可能不同。\n\n" +
        "结果永久保存在当前存档中，移除模组后仍然生效。需要回退时请载入操作前的存档。");
}
