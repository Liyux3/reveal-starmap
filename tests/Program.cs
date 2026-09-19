using System.Reflection;
using System.Runtime.Loader;
using Newtonsoft.Json;
using RevealStarMap;

string gamePath = args.Single();
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
    "Confirmation rebases on research completed while the dialog was open");
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

Console.WriteLine($"{checks} managed integration checks passed. Unity rendering, dialogs and live rocket flight require in-game testing.");
record SurveyCell(int R, int Q, float Points);
