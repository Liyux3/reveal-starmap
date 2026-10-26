using System.Reflection;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using RevealStarMap;

string gamePath = args[0];
string repo = args.Length > 1 ? args[1] : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    string path = Path.Combine(gamePath, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};

int checks = 0;
void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
    checks++;
    Console.WriteLine("PASS " + message);
}

var mod = typeof(RevealStarMapSettings).Assembly;
string version = Regex.Match(File.ReadAllText(Path.Combine(repo, "mod_info.yaml")), @"(?m)^version: (\S+)").Groups[1].Value;
Check(mod.GetName().Version!.ToString() == version, "Assembly identity matches the released package version");
Check(mod.GetManifestResourceNames().OrderBy(s => s).SequenceEqual(
    new[] { "RevealStarMap.en.json", "RevealStarMap.zh-CN.json", "RevealStarMap.zh-TW.json" }),
    "All three shipped languages are embedded for resilient offline fallback");
Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(repo, "vendor/PLib.dll")))).ToLowerInvariant() ==
    "b0144f909e00044ebd3b5880676ceb3aba756d579ea3a63aebd7a34b0777fda6",
    "Build uses the reviewed PLib binary");
using (var archive = ZipFile.OpenRead(Path.Combine(repo, "dist", "RevealStarMap-" + version + ".zip")))
{
    string package = Path.Combine(repo, "dist", "liyux.RevealStarMap");
    string[] contents = Directory.GetFiles(package, "*", SearchOption.AllDirectories)
        .Select(p => "liyux.RevealStarMap/" + Path.GetRelativePath(package, p).Replace('\\', '/')).OrderBy(p => p).ToArray();
    var entries = archive.Entries.Where(e => !e.FullName.EndsWith('/')).OrderBy(e => e.FullName).ToArray();
    Check(entries.Select(e => e.FullName).SequenceEqual(contents) && entries.All(e =>
    {
        using var stream = e.Open();
        return SHA256.HashData(stream).SequenceEqual(SHA256.HashData(File.ReadAllBytes(Path.Combine(repo, "dist", e.FullName))));
    }), "Release archive has exactly the packaged files and matching bytes");
    Check(contents.Contains("liyux.RevealStarMap/PLib.LICENSE.txt") &&
        contents.All(p => !p.Contains("LiveProbe") && !p.Contains("Assembly-CSharp") && !p.Contains("System.Security")),
        "Package includes dependency license and excludes game assemblies and test drivers");
}

// ONI's Steam installer requires a ZIP file, with mod.yaml at its root.
using (var archive = ZipFile.OpenRead(Path.Combine(repo, "dist", "RevealStarMap-" + version + "-workshop.zip")))
{
    string package = Path.Combine(repo, "dist", "liyux.RevealStarMap");
    var entries = archive.Entries.Where(e => !e.FullName.EndsWith('/')).ToArray();
    Check(archive.GetEntry("mod.yaml") != null && archive.GetEntry("mod_info.yaml") != null &&
        archive.GetEntry("RevealStarMap.dll") != null,
        "Workshop payload exposes the files required by ONI at the ZIP root");
    var expected = Directory.GetFiles(package, "*", SearchOption.AllDirectories)
        .Select(p => Path.GetRelativePath(package, p).Replace('\\', '/')).OrderBy(p => p);
    Check(entries.Select(e => e.FullName).OrderBy(p => p).SequenceEqual(expected) && entries.All(e =>
    {
        using var stream = e.Open();
        return SHA256.HashData(stream).SequenceEqual(SHA256.HashData(File.ReadAllBytes(Path.Combine(package, e.FullName))));
    }), "Legacy Workshop ZIP contains exactly the qualified package bytes");
}

// Legacy serialized lists store Q then R; AxialI's constructor takes R then Q.
var stateType = mod.GetType("RevealStarMap.State.RevealSnapshotState", true)!;
var legacyState = RuntimeHelpers.GetUninitializedObject(stateType);
void StateField(string name, object? value) => stateType.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(legacyState, value);
bool StateFlag(string name) => (bool)stateType.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(legacyState)!;
StateField("hasSnapshot", true);
StateField("snapshotQs", new List<int> { 7, -4 });
StateField("snapshotRs", new List<int> { -2, 9 });
StateField("snapshotPoints", new List<float> { 25, 140 });
var legacyMap = (Dictionary<AxialI, float>)stateType.GetMethod("RestoreSnapshot", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(legacyState, null)!;
Check(StateFlag("HasSnapshot") && legacyMap[new AxialI(-2, 7)] == 25f && legacyMap[new AxialI(9, -4)] == 140f,
    "Legacy asymmetric exploration coordinates restore with R/Q in the native order");
StateField("snapshotRs", null);
Check(!StateFlag("HasSnapshot"), "Incomplete legacy exploration snapshots are rejected safely");
StateField("hasGridSnapshot", true);
StateField("gridSnapshotWidth", 65536);
StateField("gridSnapshotHeight", 65536);
StateField("gridVisibleSnapshot", Array.Empty<byte>());
StateField("gridSpawnableSnapshot", Array.Empty<byte>());
Check(!StateFlag("HasGridSnapshot"), "Legacy canvas dimensions cannot overflow into an accepted empty snapshot");

var survey = mod.GetType("RevealStarMap.Runtime.StarMapSurvey", true)!;
var prepare = survey.GetMethod("Prepare", BindingFlags.NonPublic | BindingFlags.Static)!;
(Dictionary<AxialI, float> Map, int Changed) Prepare(Dictionary<AxialI, float> map,
    IEnumerable<AxialI> cells, float points = 100f)
{
    object?[] callArgs = { map, cells, points, null };
    var output = (Dictionary<AxialI, float>)prepare.Invoke(null, callArgs)!;
    return (output, (int)callArgs[3]!);
}

var hidden = new AxialI(0, 1);
var partial = new AxialI(1, 0);
var complete = new AxialI(2, -1);
var beyondOldDebugRadius = new AxialI(140, -10);
var unrelated = new AxialI(-1000, 400);
var cells = new[] { hidden, partial, complete, beyondOldDebugRadius, partial };
var original = new Dictionary<AxialI, float> { [partial] = 25f, [complete] = 140f, [unrelated] = 9f };
var planned = Prepare(original, cells);
Check(original.Count == 3 && original[partial] == 25f && !original.ContainsKey(hidden),
    "Preparing a proposed survey leaves the original fog dictionary untouched");
Check(planned.Changed == 3 && cells.All(c => planned.Map[c] >= 100f),
    "Hidden and partial cells become analyzed, including cells beyond radius 100");
Check(planned.Map[complete] == 140f && planned.Map[unrelated] == 9f,
    "Already earned points and unrelated entries are preserved");
Check(Prepare(planned.Map, cells).Changed == 0, "Repeating a survey is idempotent");
var addedLater = new AxialI(150, -9);
var afterGrowth = Prepare(planned.Map, cells.Append(addedLater));
Check(afterGrowth.Changed == 1 && afterGrowth.Map[addedLater] == 100f,
    "Later cluster growth is surveyed only by a subsequent explicit operation");
original[partial] = 250f;
Check(Prepare(original, cells).Map[partial] == 250f,
    "A new survey preserves research earned since the earlier preparation");
Check(Prepare(original, Array.Empty<AxialI>()).Changed == 0, "Empty clusters leave fog unchanged");
try
{
    Prepare(original, cells, float.NaN);
    throw new Exception("Invalid threshold accepted");
}
catch (TargetInvocationException e) when (e.InnerException is ArgumentOutOfRangeException)
{
    Check(true, "Invalid game tuning fails before any live state mutation");
}

var reloaded = JsonConvert.DeserializeObject<List<SurveyCell>>(JsonConvert.SerializeObject(
    planned.Map.Select(p => new SurveyCell(p.Key.r, p.Key.q, p.Value))))!;
Check(reloaded.All(p => planned.Map[new AxialI(p.R, p.Q)] == p.Points),
    "Surveyed points survive a serialization round trip without a mod-only visibility flag");
var defaults = JsonConvert.DeserializeObject<RevealStarMapSettings>("{\"Modifier\":1,\"FunctionKey\":7}")!;
Check(defaults.StarMapAction == StarMapAction.TemporaryPreview && defaults.FunctionKey == RevealFunctionKey.F7,
    "Existing configs default to reversible preview and preserve bindings");
var configured = new RevealStarMapSettings { StarMapAction = StarMapAction.PermanentSurvey };
var saved = JsonConvert.DeserializeObject<RevealStarMapSettings>(JsonConvert.SerializeObject(configured))!;
Check(saved.StarMapAction == StarMapAction.PermanentSurvey, "Survey preference persists through settings JSON");
var normalize = typeof(RevealStarMapSettings).GetMethod("Normalize", BindingFlags.NonPublic | BindingFlags.Instance)!;
saved.StarMapAction = (StarMapAction)99;
saved.Modifier = (RevealModifier)99;
saved.FunctionKey = (RevealFunctionKey)99;
normalize.Invoke(saved, null);
Check(saved.StarMapAction == StarMapAction.TemporaryPreview && saved.Modifier == RevealModifier.Shift &&
    saved.FunctionKey == RevealFunctionKey.F7, "Unknown enum values fall back to safe preview bindings");

// Exercise the shipped dynamic entry's write-back, which caused the earlier string-to-enum crash.
var entryType = mod.GetType("RevealStarMap.Options.StarMapActionOptionsEntry", true)!;
var actionEntry = Activator.CreateInstance(entryType, "StarMapAction")!;
entryType.GetProperty("Value")!.SetValue(actionEntry, "PermanentSurvey");
entryType.GetMethod("WriteTo")!.Invoke(actionEntry, new object[] { saved });
Check(saved.StarMapAction == StarMapAction.PermanentSurvey,
    "Real PLib OptionsEntry.WriteTo saves the survey enum without a string conversion crash");
var modifierType = mod.GetType("RevealStarMap.Options.RevealModifierOptionsEntry", true)!;
var modifierEntry = Activator.CreateInstance(modifierType, "Modifier")!;
modifierType.GetProperty("Value")!.SetValue(modifierEntry, "Command");
Check(modifierType.GetProperty("Value")!.GetValue(modifierEntry) is RevealModifier.Command,
    "Dropdown converts legacy strings to an actual enum before saving");
var functionType = mod.GetType("RevealStarMap.Options.RevealFunctionKeyOptionsEntry", true)!;
var functionEntry = Activator.CreateInstance(functionType, "CanvasFunctionKey")!;
functionType.GetProperty("Value")!.SetValue(functionEntry, "12");
Check(functionType.GetProperty("Value")!.GetValue(functionEntry) is RevealFunctionKey.F12,
    "Dropdown accepts persisted numeric function keys");
configured.OnOptionsChanged();
var manager = mod.GetType("RevealStarMap.RevealStarMapSettingsManager", true)!;
var activeSettings = (RevealStarMapSettings)manager.GetProperty("Current", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
Check(activeSettings.StarMapAction == StarMapAction.PermanentSurvey,
    "Saving options updates the active session immediately without loading a save or revealing cells");

var legacyMuted = JsonConvert.DeserializeObject<RevealStarMapSettings>("{\"ShowNotification\":false}")!;
Check(legacyMuted.Feedback == ActionFeedback.Off, "Old notification opt-out is preserved");
var explicitFeedback = JsonConvert.DeserializeObject<RevealStarMapSettings>("{\"Feedback\":1,\"ShowNotification\":false}")!;
Check(explicitFeedback.Feedback == ActionFeedback.EveryAction, "Explicit new feedback preference wins regardless of JSON property order");
Check(new RevealStarMapSettings().Feedback == ActionFeedback.Off &&
    new RevealStarMapSettings().StarMapAction == StarMapAction.TemporaryPreview,
    "Default behavior is silent preview for every save");
var oldDraft = JsonConvert.DeserializeObject<RevealStarMapSettings>(
    "{\"Feedback\":0,\"SurveyConfirmation\":1,\"Modifier\":3,\"FunctionKey\":9}")!;
Check(oldDraft.Feedback == ActionFeedback.Off && oldDraft.Modifier == RevealModifier.Alt &&
    oldDraft.FunctionKey == RevealFunctionKey.F9, "Old per-save preferences retire without changing shortcuts");
string globalJson = JsonConvert.SerializeObject(oldDraft);
Check(!globalJson.Contains("SurveyConfirmation") && !globalJson.Contains("ShowNotification"),
    "Settings serialize only the current global preferences");

// Exercise the game's schema migration when the unused per-save booleans disappear.
var oldTemplate = new KSerialization.SerializationTemplate(typeof(LegacyPreferences));
var newTemplate = new KSerialization.SerializationTemplate(typeof(CurrentPreviewState));
using var schemaBytes = new MemoryStream();
using var dataBytes = new MemoryStream();
using (var writer = new BinaryWriter(schemaBytes, System.Text.Encoding.UTF8, true)) oldTemplate.SerializeTemplate(writer);
using (var writer = new BinaryWriter(dataBytes, System.Text.Encoding.UTF8, true))
    oldTemplate.SerializeData(new LegacyPreferences(), writer);
var incoming = new KSerialization.DeserializationTemplate(typeof(CurrentPreviewState).AssemblyQualifiedName!, new FastReader(schemaBytes.ToArray()));
var mapping = new KSerialization.DeserializationMapping(incoming, newTemplate);
var retained = new CurrentPreviewState();
var dataReader = new FastReader(dataBytes.ToArray());
mapping.Deserialize(retained, dataReader);
Check(retained.previewMode == 2 && retained.cameraSafetyVersion == 2 && dataReader.Position == dataBytes.Length,
    "Native KSerialization skips removed preference fields and retains preview state");

var textType = mod.GetType("RevealStarMap.RevealText", true)!;
var resolve = textType.GetMethod("ResolveLanguage", BindingFlags.NonPublic | BindingFlags.Static)!;
string Resolve(UiLanguage preference, string? code) => (string)resolve.Invoke(null, new object?[] { preference, code })!;
Check(Resolve(UiLanguage.Automatic, "zh_Hant_HK") == "zh-TW" &&
    Resolve(UiLanguage.Automatic, "zh-CN") == "zh-CN" && Resolve(UiLanguage.English, "zh") == "en",
    "Chinese locale variants and explicit language override resolve consistently");
Check(Resolve(UiLanguage.Automatic, "../../test") == "en", "Locale names stay inside the translation directory");
Check(Resolve(UiLanguage.Automatic, "zh-Hans-TW") == "zh-CN" &&
    Resolve(UiLanguage.Automatic, "zh-Hant-CN") == "zh-TW",
    "Explicit Chinese script takes precedence over region");
var getText = textType.GetMethod("GetForLanguage", BindingFlags.NonPublic | BindingFlags.Static)!;
string Text(string key, string code) => (string)getText.Invoke(null, new object[] { key, code })!;
var translationDirectory = Path.Combine(AppContext.BaseDirectory, "translations");
var english = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(Path.Combine(translationDirectory, "en.json")))!;
string Slots(string text) => string.Join(",", Regex.Matches(text, @"\{\d+\}").Select(m => m.Value).OrderBy(s => s));
foreach (string code in new[] { "en", "zh-CN", "zh-TW" })
{
    var catalog = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(Path.Combine(translationDirectory, code + ".json")))!;
    Check(english.Keys.OrderBy(k => k).SequenceEqual(catalog.Keys.OrderBy(k => k)) &&
        catalog.All(p => !string.IsNullOrWhiteSpace(p.Value) && Slots(p.Value) == Slots(english[p.Key])),
        code + " catalog is complete with matching format placeholders");
    Check(catalog.All(p => Text(p.Key, code) == p.Value), code + " shipped catalog loads with exact text");
}
Check(Text("world.preview", "missing-locale") == english["world.preview"], "Missing translations fall back to embedded English");
var catalogs = (System.Collections.IDictionary)textType.GetField("Catalogs", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
string hiddenTranslations = translationDirectory + "." + Guid.NewGuid().ToString("N");
Directory.Move(translationDirectory, hiddenTranslations);
try
{
    catalogs.Clear();
    Check(Text("world.preview", "zh-CN") == "预览地图" && Text("world.preview", "zh-TW") == "預覽地圖",
        "Chinese remains translated when external catalogs are missing");
}
finally { Directory.Move(hiddenTranslations, translationDirectory); catalogs.Clear(); }
var languageEntryType = mod.GetType("RevealStarMap.Options.LanguageOptionsEntry", true)!;
var languageEntry = Activator.CreateInstance(languageEntryType, "Language")!;
languageEntryType.GetProperty("Value")!.SetValue(languageEntry, "TraditionalChinese");
languageEntryType.GetMethod("WriteTo")!.Invoke(languageEntry, new object[] { defaults });
Check(defaults.Language == UiLanguage.TraditionalChinese, "Language preference uses real enum write-back");
defaults.OnOptionsChanged();
var chineseEntry = Activator.CreateInstance(entryType, "StarMapAction")!;
Check((string)entryType.GetProperty("Title")!.GetValue(chineseEntry)! == "快捷鍵行為",
    "Reopened options use the saved Traditional Chinese language");
var category = textType.GetMethod("Category", BindingFlags.NonPublic | BindingFlags.Static)!;
foreach (var preference in new[] { UiLanguage.English, UiLanguage.SimplifiedChinese, UiLanguage.TraditionalChinese })
{
    new RevealStarMapSettings { Language = preference }.OnOptionsChanged();
    var keys = new[] { "map", "canvas", "preferences" }.Select(name => (string)category.Invoke(null, new object[] { name })!).ToArray();
    Check(keys.SequenceEqual(keys.OrderBy(k => k)) && keys.Select(k => Strings.Get(k).String).All(s => !s.StartsWith("STRINGS.")),
        preference + " options retain Starmap, Canvas, Preferences order with native labels");
}

Console.WriteLine($"{checks} managed integration checks passed. Unity rendering, dialogs and live rocket flight require in-game testing.");
record SurveyCell(int R, int Q, float Points);

[KSerialization.SerializationConfig(KSerialization.MemberSerialization.OptIn)]
class LegacyPreferences
{
    [KSerialization.Serialize] public bool notificationShown = true;
    [KSerialization.Serialize] public int previewMode = 2;
    [KSerialization.Serialize] public bool permanentSurveyConfirmed = true;
    [KSerialization.Serialize] public int cameraSafetyVersion = 2;
}

[KSerialization.SerializationConfig(KSerialization.MemberSerialization.OptIn)]
class CurrentPreviewState
{
    [KSerialization.Serialize] public int previewMode = -1;
    [KSerialization.Serialize] public int cameraSafetyVersion = -1;
}
