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
        var points = (Dictionary<AxialI, float>)AccessTools.Field(fog.GetType(), "m_revealPointsByCell").GetValue(fog);
        var camera = CameraController.Instance;
        var modeType = typeof(RevealStarMapSettings).Assembly.GetType("RevealStarMap.Runtime.RevealSessionCoordinator");
        var all = ClusterGrid.Instance.cellContents.Keys;
        return new
        {
            save = SaveLoader.GetActiveSaveFilePath(),
            mode = AccessTools.Property(modeType, "ActiveMode").GetValue(null, null).ToString(),
            analyzed = all.Count(c => ClusterGrid.Instance.IsCellVisible(c)), cells = all.Count,
            fog = string.Join(";", points.OrderBy(p => p.Key.r).ThenBy(p => p.Key.q).Select(p => p.Key.r + "," + p.Key.q + ":" + p.Value)),
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
            rockets = Components.Clustercrafts.Select(r => new { r.Name, location = r.Location.ToString(), id = r.GetInstanceID() }).ToArray()
        };
    }

    private static string Hash(byte[] bytes)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
    }
}
