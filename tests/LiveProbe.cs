// Test-only driver. Built separately and never included in dist or Workshop uploads.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PeterHan.PLib.Options;
using UnityEngine;

namespace RevealStarMap.LiveTests;

public sealed class ProbeMod : KMod.UserMod2
{
    public override void OnLoad(Harmony harmony)
    {
        Application.runInBackground = true;
        base.OnLoad(harmony);
        Debug.Log("[Reveal Live Test] Driver patches registered.");
    }

    [HarmonyPatch(typeof(SaveLoader), nameof(SaveLoader.Save), new[] { typeof(string), typeof(bool), typeof(bool) })]
    public static class IsolateSaveWrites
    {
        public static void Prefix([HarmonyArgument(0)] ref string filename)
        {
            string root = Path.GetFullPath(Driver.Root).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (Path.GetFullPath(filename).StartsWith(root, StringComparison.Ordinal)) return;
            // Includes native autosaves while a copied campaign's simulation advances.
            string folder = Path.Combine(Driver.Root, "auto_save");
            Directory.CreateDirectory(folder);
            filename = Path.Combine(folder, Path.GetFileName(filename));
            Debug.Log("[Reveal Live Test] Redirected a save to " + filename);
        }
    }

    [HarmonyPatch(typeof(Steamworks.SteamUserStats))]
    public static class IsolateAccountStats
    {
        public static IEnumerable<MethodBase> TargetMethods() => typeof(Steamworks.SteamUserStats)
            .GetMethods(BindingFlags.Public | BindingFlags.Static).Where(method => method.ReturnType == typeof(bool)
                && new[] { "SetAchievement", "ClearAchievement", "SetStat", "StoreStats", "ResetAllStats" }.Contains(method.Name));

        public static bool Prefix(ref bool __result)
        {
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(MainMenu), "OnSpawn")]
    public static class MainMenuProbe
    {
      public static void Postfix()
      {
        if (UnityEngine.Object.FindObjectOfType<Driver>() != null) return;
        var go = new GameObject("RevealStarMapLiveProbe");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<Driver>();
        Debug.Log("[Reveal Live Test] Driver started in main menu.");
      }
    }

    public override void OnAllModsLoaded(Harmony harmony, IReadOnlyList<KMod.Mod> mods)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("PeterHan.PLib.Options.OptionsDialog");
            var method = t == null ? null : AccessTools.Method(t, "ShowDialog");
            if (method != null)
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(Driver), nameof(Driver.RecordDialog)));
        }
    }
}

public sealed class Driver : MonoBehaviour
{
    internal static readonly string Root = File.ReadAllText(Path.Combine(
        Path.GetDirectoryName(typeof(Driver).Assembly.Location), "probe-root.txt")).Trim();
    private string lastId = "";
    private float nextPoll;
    private SaveGame lastSave;
    private static object options;

    public static void RecordDialog(object __instance)
    {
        var type = (Type)AccessTools.Field(__instance.GetType(), "optionsType").GetValue(__instance);
        if (type == typeof(RevealStarMapSettings)) options = __instance;
    }

    private void Update()
    {
        if (SaveGame.Instance != null && !Game.Instance.IsLoading() && SaveGame.Instance != lastSave)
        {
            lastSave = SaveGame.Instance;
            SpeedControlScreen.Instance.Pause(false);
        }
        if (Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + 0.25f;
        string command = Path.Combine(Root, "command.json");
        if (!File.Exists(command)) return;
        JObject input;
        try { input = JObject.Parse(File.ReadAllText(command)); }
        catch { return; }
        string id = (string)input["id"];
        if (id == lastId) return;
        lastId = id;
        try { Reply(id, true, Run(input)); }
        catch (Exception e) { Reply(id, false, e.ToString()); }
    }

    private static void Reply(string id, bool ok, object result) => File.WriteAllText(
        Path.Combine(Root, "result.json"), JsonConvert.SerializeObject(new { id, ok, result }, Formatting.Indented));

    private static object Call(string type, string method, params object[] args) =>
        AccessTools.Method(typeof(RevealStarMapSettings).Assembly.GetType("RevealStarMap.Runtime." + type), method).Invoke(null, args);

    private static string TestSave(string name)
    {
        if (Path.GetFileName(name) != name || !name.EndsWith(".sav")) throw new ArgumentException("Test save basename required");
        return Path.Combine(Root, name);
    }

    private static void RequireLoadedGame()
    {
        if (Game.Instance == null || SaveGame.Instance == null || Game.Instance.IsLoading() || ClusterGrid.Instance == null)
            throw new InvalidOperationException("A fully loaded game is required");
    }

    private static string RequireTestSave()
    {
        RequireLoadedGame();
        string active = SaveLoader.GetActiveSaveFilePath();
        if (string.IsNullOrEmpty(active)) throw new InvalidOperationException("Active test save required");
        string save = Path.GetFullPath(active);
        string root = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!save.StartsWith(root, StringComparison.Ordinal) || !File.Exists(save))
            throw new InvalidOperationException("Active save must exist within Driver.Root");
        // Reject links below the trusted root, including a linked save file.
        for (string entry = save; entry.Length >= root.Length; entry = Path.GetDirectoryName(entry))
            if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Active test save path contains a symbolic link");
        return save;
    }

    private static Dictionary<AxialI, float> RevealPoints(ClusterFogOfWarManager.Instance fog) =>
        (Dictionary<AxialI, float>)AccessTools.Field(fog.GetType(), "m_revealPointsByCell").GetValue(fog);

    private static object Research(JObject c)
    {
        string save = RequireTestSave();
        if (c["points"] == null || (c["points"].Type != JTokenType.Integer && c["points"].Type != JTokenType.Float))
            throw new ArgumentException("Finite positive numeric points required");
        float amount = (float)c["points"];
        if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f)
            throw new ArgumentException("Finite positive points required");
        if (c["r"]?.Type != JTokenType.Integer || c["q"]?.Type != JTokenType.Integer)
            throw new ArgumentException("Integer target r and q required");
        var target = new AxialI((int)c["r"], (int)c["q"]);
        if (!ClusterGrid.Instance.IsValidCell(target)) throw new ArgumentException("Invalid research target");
        var fog = SaveGame.Instance.GetSMI<ClusterFogOfWarManager.Instance>();
        if (fog == null) throw new InvalidOperationException("Native fog manager unavailable");
        var points = RevealPoints(fog);
        float before, after;
        bool beforeHadEntry = points.TryGetValue(target, out before);
        bool revealCompleted = fog.EarnRevealPointsForLocation(target, amount);
        bool afterHadEntry = points.TryGetValue(target, out after);
        return new { save, r = target.r, q = target.q, requestedPoints = amount,
            beforeHadEntry, beforePoints = before, afterHadEntry, afterPoints = after, revealCompleted };
    }

    private static object RocketReadiness(Clustercraft rocket)
    {
        var conditions = new List<object>();
        bool? prepped = null, ready = null;
        string gap = null, reading = "CraftModuleInterface";
        try
        {
            var modules = rocket.ModuleInterface;
            if (modules == null) throw new InvalidOperationException("Native module interface unavailable");
            // Matches CraftModuleInterface.CheckReadyToLaunch, including launchpad conditions.
            foreach (var group in new[] { ProcessCondition.ProcessConditionType.RocketPrep,
                ProcessCondition.ProcessConditionType.RocketStorage, ProcessCondition.ProcessConditionType.RocketFlight,
                ProcessCondition.ProcessConditionType.RocketBoard })
            {
                reading = group.ToString();
                var native = new List<ProcessCondition>();
                modules.PopulateConditionSet(group, native);
                foreach (var condition in native)
                {
                    reading = group + "/" + condition.GetType().FullName;
                    var status = condition.EvaluateCondition();
                    conditions.Add(new { group = group.ToString(), type = condition.GetType().FullName,
                        status = status.ToString(), message = condition.GetStatusMessage(status),
                        tooltip = condition.GetStatusTooltip(status), shownInUI = condition.ShowInUI() });
                }
            }
            reading = "CheckPreppedForLaunch/CheckReadyToLaunch";
            prepped = rocket.CheckPreppedForLaunch();
            ready = rocket.CheckReadyToLaunch();
        }
        catch (Exception e) { gap = reading + ": " + e.GetType().Name + ": " + e.Message; }
        return new { id = rocket.GetInstanceID(), rocket.Name, status = rocket.Status.ToString(),
            available = gap == null, preppedForLaunch = prepped, readyToLaunch = ready,
            launchRequested = rocket.LaunchRequested, instantBuild = DebugHandler.InstantBuildMode, conditions, gap };
    }

    private static object Run(JObject c)
    {
        switch ((string)c["action"])
        {
            case "batch":
                return ((JArray)c["commands"]).Select(command => Run((JObject)command)).ToArray();
            case "ui":
                return UnityEngine.Object.FindObjectsOfType<KButton>().Where(b => b.gameObject.activeInHierarchy)
                    .Select(b => new { id = b.GetInstanceID(), name = b.name, active = b.isInteractable,
                        text = string.Join(" / ", b.GetComponentsInChildren<LocText>().Select(t => t.text)),
                        pos = b.transform.position.ToString(), focused = KInputManager.isFocused }).ToArray();
            case "click":
                var button = UnityEngine.Object.FindObjectsOfType<KButton>().Single(b => b.GetInstanceID() == (int)c["button"]);
                button.SignalClick(KKeyCode.Mouse0);
                return button.name;
            case "options":
                POptions.ShowDialog(typeof(RevealStarMapSettings));
                return "Opened real PLib options dialog";
            case "option":
                var cats = (IEnumerable)AccessTools.Field(options.GetType(), "optionCategories").GetValue(options);
                foreach (var cat in cats)
                foreach (var entry in (IEnumerable)cat.GetType().GetProperty("Value").GetValue(cat, null))
                {
                    if ((string)entry.GetType().GetProperty("Field").GetValue(entry, null) != (string)c["field"]) continue;
                    entry.GetType().GetProperty("Value").SetValue(entry, (string)c["value"], null);
                    return new { field = (string)c["field"], value = entry.GetType().GetProperty("Value").GetValue(entry, null) };
                }
                throw new Exception("Option not found");
            case "load":
                LoadingOverlay.Load(() => LoadScreen.DoLoad(TestSave((string)c["name"])));
                return "Loading isolated test save";
            case "save":
                return SaveLoader.Instance.Save(TestSave((string)c["name"]), false, false);
            case "snapshot": return Snapshot();
            case "research": return Research(c);
            case "readiness":
                RequireLoadedGame();
                return c["rocket"] == null ? (object)Components.Clustercrafts.Select(RocketReadiness).ToArray() :
                    RocketReadiness(Components.Clustercrafts.Single(r => r.GetInstanceID() == (int)c["rocket"]));
            case "pause":
                SpeedControlScreen.Instance.Pause(false);
                return Snapshot();
            case "flight":
                RequireTestSave();
                if (DebugHandler.InstantBuildMode) throw new InvalidOperationException("Disable native instant-build mode before flight");
                var flying = Components.Clustercrafts.Single(r => r.GetInstanceID() == (int)c["rocket"]);
                var flightSelector = flying.GetComponent<RocketClusterDestinationSelector>();
                var destination = new AxialI((int)c["r"], (int)c["q"]);
                if (flightSelector == null || !ClusterGrid.Instance.IsValidCell(destination))
                    throw new ArgumentException("Native rocket selector and valid destination required");
                if (flying.Status != Clustercraft.CraftStatus.Grounded && flying.Status != Clustercraft.CraftStatus.InFlight)
                    throw new InvalidOperationException("Rocket must be grounded or in flight");
                string failure;
                var flightPath = ClusterGrid.Instance.GetPath(flying.Location, destination, flightSelector, out failure);
                // Native GetPath omits the starting cell; Count is the travel distance.
                if (flightPath == null || flightPath.Count == 0 || flightPath.Count > flying.GetRangeInTiles())
                    throw new InvalidOperationException("Native rocket route/fuel check failed: " + failure);
                var previous = flying.Destination;
                flightSelector.SetDestination(destination);
                if (flying.Status == Clustercraft.CraftStatus.Grounded)
                {
                    // RequestLaunch starts normal boarding; Launch later checks RocketBoard too.
                    if (!flying.CheckPreppedForLaunch())
                    {
                        string diagnostic = JsonConvert.SerializeObject(RocketReadiness(flying));
                        flightSelector.SetDestination(previous);
                        throw new InvalidOperationException("Native launch preparation check failed: " + diagnostic);
                    }
                    flying.RequestLaunch();
                    if (flying.Status == Clustercraft.CraftStatus.Grounded && !flying.LaunchRequested)
                    {
                        string diagnostic = JsonConvert.SerializeObject(RocketReadiness(flying));
                        flightSelector.SetDestination(previous);
                        throw new InvalidOperationException("Native launch request declined: " + diagnostic);
                    }
                }
                SpeedControlScreen.Instance.SetSpeed(2);
                SpeedControlScreen.Instance.Unpause(false);
                return new { id = flying.GetInstanceID(), rocket = flying.Name, r = flying.Location.r, q = flying.Location.q,
                    targetR = destination.r, targetQ = destination.q, steps = flightPath.Count,
                    status = flying.Status.ToString(), launchRequested = flying.LaunchRequested };
            case "route":
                var craft = Components.Clustercrafts.FirstOrDefault();
                var fixture = craft == null ? new GameObject("TestRouteSelector") : null;
                var selector = craft != null ? craft.GetComponent<RocketClusterDestinationSelector>() : fixture.AddComponent<ClusterDestinationSelector>();
                var start = craft != null ? craft.Location : ClusterManager.Instance.GetStartWorld().GetComponent<ClusterGridEntity>().Location;
                var target = c["r"] != null ? new AxialI((int)c["r"], (int)c["q"]) :
                    ClusterGrid.Instance.cellContents.Keys.First(cell => !ClusterGrid.Instance.IsCellVisible(cell) && ClusterGrid.Instance.GetAsteroidAtCell(cell) == null);
                string reason;
                var path = ClusterGrid.Instance.GetPath(start, target, selector, out reason);
                if (fixture != null) UnityEngine.Object.Destroy(fixture);
                return new { actualRocket = craft != null, start = start.ToString(), targetR = target.r, targetQ = target.q, steps = path?.Count, reason };
            case "resume-pointer":
                string resume = (string)c["path"];
                if (!Path.IsPathRooted(resume) || !File.Exists(resume)) throw new ArgumentException("Existing original save path required");
                SaveLoader.SetActiveSaveFilePath(resume);
                return "Original resume pointer restored";
            case "starmap":
                Call("RevealStarMapRuntime", "TriggerStarmapHotkeyAction");
                return Snapshot();
            case "canvas":
                Call("RevealSessionCoordinator", "ToggleCanvas");
                return Snapshot();
            case "world":
                Call("RevealSessionCoordinator", "ToggleWorld", ClusterManager.Instance.GetWorld((int)c["world"]));
                return "World preview requested";
            case "world-ui":
                ClusterMapScreen.Instance.SelectEntity(ClusterManager.Instance.GetWorld((int)c["world"]).GetComponent<ClusterGridEntity>());
                return "World side screen targeted";
            case "show-map":
                ManagementMenu.Instance.ToggleClusterMap();
                return "Starmap toggled";
            case "camera":
                CameraController.Instance.SnapTo(new Vector3((float)c["x"], (float)c["y"], -100), (float)c["zoom"]);
                return "Camera moved for bounds test";
            case "quit":
                App.Quit();
                return "Graceful quit requested";
            default: throw new ArgumentException("Unknown command");
        }
    }

    private static object Snapshot()
    {
        if (Game.Instance == null || SaveGame.Instance == null || Game.Instance.IsLoading()) return new { loading = true };
        var fog = SaveGame.Instance.GetSMI<ClusterFogOfWarManager.Instance>();
        if (fog == null || ClusterGrid.Instance == null || CameraController.Instance == null || GameClock.Instance == null)
            return new { loading = true };
        var points = RevealPoints(fog);
        var camera = CameraController.Instance;
        var modeType = typeof(RevealStarMapSettings).Assembly.GetType("RevealStarMap.Runtime.RevealSessionCoordinator");
        var all = ClusterGrid.Instance.cellContents.Keys;
        return new
        {
            save = SaveLoader.GetActiveSaveFilePath(),
            cycle = GameClock.Instance.GetCycle(), simTimeSeconds = GameClock.Instance.GetTime(),
            version = typeof(RevealStarMapSettings).Assembly.GetName().Version.ToString(),
            mapOpen = ClusterMapScreen.Instance != null && ClusterMapScreen.Instance.IsScreenActive(),
            gridWidth = Grid.WidthInCells, gridHeight = Grid.HeightInCells,
            mode = AccessTools.Property(modeType, "ActiveMode").GetValue(null, null).ToString(),
            analyzed = all.Count(c => ClusterGrid.Instance.IsCellVisible(c)), cells = all.Count,
            fog = string.Join(";", points.OrderBy(p => p.Key.r).ThenBy(p => p.Key.q).Select(p => p.Key.r + "," + p.Key.q + ":" + p.Value)),
            revealPoints = points.OrderBy(p => p.Key.r).ThenBy(p => p.Key.q)
                .Select(p => new { r = p.Key.r, q = p.Key.q, points = p.Value }).ToArray(),
            visible = Hash(Grid.Visible), spawnable = Hash(Grid.Spawnable),
            debug = Game.Instance.debugWasUsed, sandbox = SaveGame.Instance.sandboxEnabled,
            camera = new { position = camera.transform.position.ToString(), zoom = camera.OrthographicSize,
                targetZoom = camera.targetOrthographicSize, free = camera.FreeCameraEnabled, fx = camera.ignoreClusterFX,
                fading = camera.activeFadeRoutine != null, inputDisabled = camera.DisableUserCameraControl,
                focused = Application.isFocused, hotkeysReady = Call("RevealInput", "CanProcessHotkeys"),
                maxZoom = AccessTools.Field(typeof(CameraController), "maxOrthographicSize").GetValue(camera) },
            worlds = ClusterManager.Instance.WorldContainers.Select(w => new { w.id, w.worldName, w.IsDiscovered, w.IsDupeVisited,
                w.IsRoverVisted, offset = w.WorldOffset.ToString(), size = w.WorldSize.ToString() }).ToArray(),
            activeWorld = ClusterManager.Instance.activeWorldId,
            rockets = Components.Clustercrafts.Select(RocketSnapshot).ToArray()
        };
    }

    private static object RocketSnapshot(Clustercraft rocket)
    {
        var selector = rocket.GetComponent<RocketClusterDestinationSelector>();
        var traveler = rocket.GetComponent<ClusterTraveler>();
        var path = traveler?.CurrentPath;
        var pad = rocket.ModuleInterface.CurrentPad;
        return new { rocket.Name, r = rocket.Location.r, q = rocket.Location.q,
            targetR = rocket.Destination.r, targetQ = rocket.Destination.q, status = rocket.Status.ToString(),
            traveling = rocket.IsFlightInProgress(), range = rocket.GetRangeInTiles(), speed = rocket.Speed,
            id = rocket.GetInstanceID(), launchRequested = rocket.LaunchRequested,
            atDestination = selector == null ? (bool?)null : selector.IsAtDestination(),
            destinationWorld = selector?.GetDestinationWorld(), padWorld = pad == null ? (int?)null : pad.GetMyWorldId(),
            repeat = selector == null ? (bool?)null : selector.Repeat,
            pathAvailable = path != null, remainingNodes = path?.Count,
            moveProgress = traveler?.GetMoveProgress(), etaSeconds = traveler?.TravelETA() };
    }

    private static string Hash(byte[] bytes)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
    }
}
