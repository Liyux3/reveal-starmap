using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace RevealStarMap.Runtime.Cards;

internal enum CardFinish { Plain, Etched, Holo, Gilded, Frost, Molten, Tidal, Aurora, Golden, Shadow, Misprint }

internal sealed class CardGeyser
{
    internal string Prefab = string.Empty;
    internal string Name = string.Empty;
    internal int X, Y, Count = 1;
    internal float? GramsPerSecond;
}

// Everything a card states about one asteroid, read from the game. Tiers and hidden variants
// depend only on the seed and the world, so the same seed always prints the same card.
internal sealed class CardData
{
    private static readonly HashSet<string> Metals = new HashSet<string>
        { "molten_copper", "molten_iron", "molten_gold", "molten_aluminum", "molten_cobalt", "molten_tungsten", "molten_niobium" };

    internal WorldContainer World = null!;
    internal string Name = string.Empty, Description = string.Empty, Seed = string.Empty, SetCode = string.Empty, Colony = string.Empty;
    internal string[] Traits = Array.Empty<string>();
    internal List<CardGeyser> Geysers = new List<CardGeyser>();
    internal List<(ProcGen.SubWorld.ZoneType Zone, float Share)> Biomes = new List<(ProcGen.SubWorld.ZoneType, float)>();
    internal int Cycle, Index, SetSize, HiddenRank = 1;
    internal bool Start, Revealed;
    internal uint Hash;

    internal IEnumerable<string> Kinds => Geysers.Select(g => g.Prefab.Replace("GeyserGeneric_", ""));

    internal static uint HashOf(string seed, string world)
    {
        using var sha = SHA256.Create();
        byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(seed + "|" + world));
        return (uint)(h[0] << 24 | h[1] << 16 | h[2] << 8 | h[3]);
    }

    internal static string SetCodeOf(string seed)
    {
        Match m = Regex.Match(seed, "^[A-Z]+-([A-Z0-9]+-[A-Z])");
        return m.Success ? m.Groups[1].Value : seed.Split('-').FirstOrDefault() ?? seed;
    }

    internal string Tier
    {
        get
        {
            var kinds = new HashSet<string>(Kinds);
            int metal = kinds.Count(Metals.Contains);
            if (Start || metal >= 3) return "Ultra";
            if (metal > 0 || kinds.Count >= 5) return "Rare";
            if (kinds.Count >= 2 || Geysers.Sum(g => g.Count) >= 3) return "Uncommon";
            return "Common";
        }
    }

    internal CardFinish Finish => FinishOf(Hash, Tier, Biomes.Count > 0 ? Biomes[0] : default);

    internal static CardFinish FinishOf(uint h, string tier, (ProcGen.SubWorld.ZoneType Zone, float Share) lead)
    {
        if (h % 100 == 2) return CardFinish.Misprint;
        if (h % 64 == 3) return CardFinish.Golden;
        if (h % 48 == 5) return CardFinish.Shadow;
        if (h % 16 == 0) return CardFinish.Aurora;
        if (tier == "Ultra") return CardFinish.Gilded;
        if (tier == "Rare") return CardFinish.Holo;
        if (lead.Share >= 0.6f)
        {
            if (lead.Zone == ProcGen.SubWorld.ZoneType.FrozenWastes || lead.Zone == ProcGen.SubWorld.ZoneType.IceCaves) return CardFinish.Frost;
            if (lead.Zone == ProcGen.SubWorld.ZoneType.MagmaCore) return CardFinish.Molten;
            if (lead.Zone == ProcGen.SubWorld.ZoneType.Ocean) return CardFinish.Tidal;
        }
        return tier == "Uncommon" ? CardFinish.Etched : CardFinish.Plain;
    }

    internal static bool IsHidden(CardFinish f) => f >= CardFinish.Aurora;

    internal int Serial => (int)(Hash % 9999) + 1;

    internal static List<CardData> ReadCluster(bool revealed)
    {
        var worlds = ClusterManager.Instance.WorldContainers.Where(w => w != null && !w.IsModuleInterior)
            .OrderBy(w => !w.IsStartWorld).ThenByDescending(w => w.WorldSize.x * w.WorldSize.y).ToList();
        var cards = worlds.Select((w, i) => Read(w, i + 1, worlds.Count, revealed)).ToList();
        int rank = 0;
        foreach (CardData card in cards) if (IsHidden(card.Finish)) card.HiddenRank = ++rank;
        return cards;
    }

    internal static CardData Read(WorldContainer world, int index, int setSize, bool revealed)
    {
        string seed = CustomGameSettings.Instance != null ? CustomGameSettings.Instance.GetSettingsCoordinate() : string.Empty;
        var card = new CardData
        {
            World = world, Name = world.GetProperName(), Seed = seed, SetCode = SetCodeOf(seed),
            Colony = SaveGame.Instance?.BaseName ?? string.Empty, Cycle = GameClock.Instance.GetCycle() + 1,
            Index = index, SetSize = setSize, Start = world.IsStartWorld, Revealed = revealed,
            Description = Regex.Replace(Strings.Get(world.worldDescription).String.Split(new[] { "\n\n" }, StringSplitOptions.None)[0], "<[^>]+>", "").Trim(),
            Traits = world.WorldTraitIds?.Select(t => Strings.Get(ProcGen.SettingsCache.GetCachedWorldTrait(t, false)?.name ?? t).String).ToArray() ?? Array.Empty<string>()
        };
        card.Hash = HashOf(seed, card.Name);
        card.Geysers = ReadGeysers(world);
        card.Biomes = ReadBiomes(world);
        return card;
    }

    private static List<CardGeyser> ReadGeysers(WorldContainer world)
    {
        var found = new List<CardGeyser>();
        var seen = new HashSet<int>();
        foreach (Geyser g in UnityEngine.Object.FindObjectsByType<Geyser>(FindObjectsSortMode.None))
        {
            int cell = Grid.PosToCell(g);
            if (!Grid.IsValidCell(cell) || Grid.WorldIdx[cell] != world.id) continue;
            Vector2I xy = Grid.CellToXY(cell);
            seen.Add(cell);
            found.Add(new CardGeyser { Prefab = g.GetComponent<KPrefabID>().PrefabTag.Name, Name = g.GetProperName(), X = xy.x, Y = xy.y,
                GramsPerSecond = g.configuration.GetAverageEmission() * 1000f });
        }
        // Unexplored areas keep their worldgen spawns pending; the type is known, the output is not.
        var spawner = UnityEngine.Object.FindAnyObjectByType<WorldGenSpawner>();
        if (spawner != null && AccessTools.Field(typeof(WorldGenSpawner), "spawnables")?.GetValue(spawner) is IEnumerable pending)
            foreach (object o in pending)
            {
                if (!(AccessTools.Property(o.GetType(), "spawnInfo")?.GetValue(o) is TemplateClasses.Prefab p) || !p.id.StartsWith("Geyser")) continue;
                int cell = Grid.XYToCell(p.location_x, p.location_y);
                if (!Grid.IsValidCell(cell) || Grid.WorldIdx[cell] != world.id || seen.Contains(cell)) continue;
                GameObject? prefab = Assets.TryGetPrefab(p.id);
                found.Add(new CardGeyser { Prefab = p.id, Name = prefab != null ? prefab.GetProperName() : p.id, X = p.location_x, Y = p.location_y });
            }
        // Analyzed first, then known types, then generic spawns whose type is still open.
        return found.OrderBy(g => g.GramsPerSecond == null).ThenBy(g => g.Prefab == "GeyserGeneric").ThenBy(g => g.Name).ToList();
    }

    private static List<(ProcGen.SubWorld.ZoneType, float)> ReadBiomes(WorldContainer world)
    {
        var counts = new Dictionary<ProcGen.SubWorld.ZoneType, int>();
        var zones = global::World.Instance.zoneRenderData;
        int total = 0;
        Vector2I o = world.WorldOffset, s = world.WorldSize;
        for (int y = o.y; y < o.y + s.y; y += 2)
            for (int x = o.x; x < o.x + s.x; x += 2)
            {
                var z = zones.GetSubWorldZoneType(Grid.XYToCell(x, y));
                if (z == ProcGen.SubWorld.ZoneType.Space) continue;
                counts[z] = counts.TryGetValue(z, out int c) ? c + 1 : 1;
                total++;
            }
        return counts.OrderByDescending(kv => kv.Value).Select(kv => (kv.Key, kv.Value / (float)Math.Max(1, total))).ToList();
    }

    // One tile per kind with its count, as the card's moves.
    internal List<CardGeyser> Tiles()
    {
        var tiles = new List<CardGeyser>();
        foreach (CardGeyser g in Geysers)
        {
            CardGeyser? same = tiles.FirstOrDefault(t => t.Prefab == g.Prefab);
            if (same != null) { same.Count++; continue; }
            tiles.Add(new CardGeyser { Prefab = g.Prefab, Name = g.Name, X = g.X, Y = g.Y, GramsPerSecond = g.GramsPerSecond });
        }
        return tiles;
    }
}
