using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kitchen;
using Kitchen.Layouts;
using Kitchen.Layouts.Features;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace MorePlayers
{
    // Restaurant maps are built by LayoutGraph.Build and then decorated (tables, kitchen equipment).
    // While a real restaurant map is being constructed, stretch the finished blueprint by duplicating
    // rows/columns that run through the kitchen or dining room. Walls are generated wherever two
    // different rooms meet, so duplicated tiles simply extend each room; doors and hatches are shifted
    // so they stay between the same pair of tiles.
    [HarmonyPatch(typeof(CreateLayoutHelper), nameof(CreateLayoutHelper.ConstructLayout))]
    public class ConstructLayoutOverridePatch
    {
        private const int VanillaMaxPlayers = 4;

        // After this many failed attempts, stop stretching so vanilla generation can still succeed.
        private const int StretchAttempts = 50;

        // Hard cap on extra tiles per axis.
        private const int MaxExtraPerAxis = 6;

        internal static bool Constructing;
        internal static int TargetPlayers;
        internal static int Attempt;

        [HarmonyPrefix]
        public static void Prefix(EntityManager em)
        {
            Constructing = false;
            if (!Config.ConfigHelper.getBiggerLayouts())
            {
                return;
            }
            TargetPlayers = LayoutPlayers(em);
            Attempt = 0;
            Constructing = TargetPlayers > VanillaMaxPlayers;
        }

        // How many players new maps should be sized for: players in the lobby (or the configured minimum), capped at the max.
        internal static int LayoutPlayers(EntityManager em)
        {
            int live_players;
            using (EntityQuery query = em.CreateEntityQuery(ComponentType.ReadOnly<CPlayer>(), ComponentType.Exclude<CJoiningPlayer>()))
            {
                live_players = query.CalculateEntityCount();
            }
            int players = Math.Max(live_players, Config.ConfigHelper.getLayoutSizePlayers());
            return Math.Min(players, Config.ConfigHelper.getMaxPlayers());
        }

        [HarmonyFinalizer]
        public static void Finalizer()
        {
            Constructing = false;
        }

        internal static void Stretch(LayoutBlueprint blueprint)
        {
            Attempt++;
            if (blueprint == null || Attempt > StretchAttempts)
            {
                return;
            }

            List<LayoutPosition> interior = blueprint.Tiles.Where(t => IsPlayArea(t.Value)).Select(t => t.Key).ToList();
            if (interior.Count == 0)
            {
                return;
            }
            int width = interior.Max(p => p.x) - interior.Min(p => p.x) + 1;
            int height = interior.Max(p => p.y) - interior.Min(p => p.y) + 1;

            // Scale area in proportion to players: each axis by sqrt(players / 4).
            float axis_scale = Mathf.Sqrt((float)TargetPlayers / VanillaMaxPlayers);
            int extra_x = Mathf.Clamp(Mathf.RoundToInt(width * (axis_scale - 1f)), 0, MaxExtraPerAxis);
            int extra_y = Mathf.Clamp(Mathf.RoundToInt(height * (axis_scale - 1f)), 0, MaxExtraPerAxis);

            for (int i = 0; i < extra_x; i++)
            {
                DuplicateLine(blueprint, is_row: false);
            }
            for (int i = 0; i < extra_y; i++)
            {
                DuplicateLine(blueprint, is_row: true);
            }

            // The new lines were inserted inside the layout, so its far edge moved out. Shift back by half to keep it centred.
            LayoutPosition offset = new LayoutPosition(-(extra_x / 2), -(extra_y / 2));
            blueprint.Tiles = blueprint.Tiles.ToDictionary(t => Add(t.Key, offset), t => t.Value);
            blueprint.Features = blueprint.Features.Select(f => new Feature(Add(f.Tile1, offset), Add(f.Tile2, offset), f.Type)).ToList();

            MorePlayers.Log.LogInfo($"Layout for {TargetPlayers} players (attempt {Attempt}): play area {width}x{height} -> {width + extra_x}x{height + extra_y}");
        }

        private static bool IsPlayArea(Room room)
        {
            return room.Type == RoomType.Kitchen || room.Type == RoomType.Dining;
        }

        // Duplicates one row (or column) that passes through the kitchen/dining area: the copy is inserted
        // directly after it and everything beyond shifts out by one.
        private static void DuplicateLine(LayoutBlueprint blueprint, bool is_row)
        {
            List<int> candidates = blueprint.Tiles.Where(t => IsPlayArea(t.Value))
                .Select(t => is_row ? t.Key.y : t.Key.x).Distinct().ToList();
            if (candidates.Count == 0)
            {
                return;
            }
            int line = candidates[UnityEngine.Random.Range(0, candidates.Count)];

            Dictionary<LayoutPosition, Room> tiles = new Dictionary<LayoutPosition, Room>();
            foreach (KeyValuePair<LayoutPosition, Room> tile in blueprint.Tiles)
            {
                tiles[Shift(tile.Key, is_row, line)] = tile.Value;
                if (Along(tile.Key, is_row) == line)
                {
                    tiles[Shift(tile.Key, is_row, line, copy: true)] = tile.Value;
                }
            }
            blueprint.Tiles = tiles;

            // A feature that crosses the seam (one tile on the line, the other just past it) now sits
            // between the copy and the shifted tile. Features lying along the line stay on the original only,
            // which at worst leaves a plain wall in the copied line.
            blueprint.Features = blueprint.Features.Select(f =>
            {
                int a = Along(f.Tile1, is_row);
                int b = Along(f.Tile2, is_row);
                bool crosses = (a == line && b == line + 1) || (b == line && a == line + 1);
                LayoutPosition t1 = crosses && a == line ? Shift(f.Tile1, is_row, line, copy: true) : Shift(f.Tile1, is_row, line);
                LayoutPosition t2 = crosses && b == line ? Shift(f.Tile2, is_row, line, copy: true) : Shift(f.Tile2, is_row, line);
                return new Feature(t1, t2, f.Type);
            }).ToList();
        }

        private static int Along(LayoutPosition p, bool is_row)
        {
            return is_row ? p.y : p.x;
        }

        private static LayoutPosition Shift(LayoutPosition p, bool is_row, int line, bool copy = false)
        {
            int along = Along(p, is_row);
            int delta = (copy || along > line) ? 1 : 0;
            return is_row ? new LayoutPosition(p.x, p.y + delta) : new LayoutPosition(p.x + delta, p.y);
        }

        private static LayoutPosition Add(LayoutPosition p, LayoutPosition offset)
        {
            return new LayoutPosition(p.x + offset.x, p.y + offset.y);
        }
    }

    // The HQ generates its restaurant maps as soon as it loads, before friends have joined. When the lobby
    // grows past what the current maps were sized for, re-raise the game's own "regenerate maps" request
    // (the same thing changing the restaurant setting does). Waits until the player count has been stable
    // for a few seconds and no one is carrying a map.
    [HarmonyPatch(typeof(HandleLayoutRequests), "OnUpdate")]
    public class HandleLayoutRequestsOverridePatch
    {
        private const float StableSeconds = 3f;

        private static int GeneratedFor = -1;
        private static int PendingPlayers = -1;
        private static float PendingSince;

        [HarmonyPrefix]
        public static void Prefix(HandleLayoutRequests __instance)
        {
            if (!Config.ConfigHelper.getBiggerLayouts())
            {
                return;
            }
            Traverse traverse = Traverse.Create(__instance);
            EntityQuery requests = traverse.Field("Requests").GetValue<EntityQuery>();
            if (requests.CalculateEntityCount() != 1)
            {
                return;
            }
            EntityManager em = __instance.EntityManager;
            int players = ConstructLayoutOverridePatch.LayoutPlayers(em);
            HandleLayoutRequests.SLayoutRequest request = requests.GetSingleton<HandleLayoutRequests.SLayoutRequest>();

            if (!request.HasBeenCreated)
            {
                // The game is about to (re)generate the maps this frame, sized for the current lobby.
                GeneratedFor = players;
                PendingPlayers = -1;
                return;
            }
            if (players <= GeneratedFor || players <= 4)
            {
                PendingPlayers = -1;
                return;
            }
            if (players != PendingPlayers)
            {
                PendingPlayers = players;
                PendingSince = Time.realtimeSinceStartup;
                return;
            }
            if (Time.realtimeSinceStartup - PendingSince < StableSeconds || AnyMapOutOfSlot(em, traverse))
            {
                return;
            }

            MorePlayers.Log.LogInfo($"Lobby has {players} players (maps were sized for {GeneratedFor}), regenerating restaurant maps");
            request.HasBeenCreated = false;
            requests.SetSingleton(request);
        }

        // True if any generated map has been picked up or moved off its pedestal.
        private static bool AnyMapOutOfSlot(EntityManager em, Traverse traverse)
        {
            EntityQuery map_items = traverse.Field("MapItems").GetValue<EntityQuery>();
            using (NativeArray<Entity> maps = map_items.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity map in maps)
                {
                    if (!em.HasComponent<CHeldBy>(map))
                    {
                        return true;
                    }
                    Entity holder = em.GetComponentData<CHeldBy>(map).Holder;
                    if (!em.Exists(holder) || !em.HasComponent<CreateLayoutSlots.CLayoutSlot>(holder))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(LayoutGraph), nameof(LayoutGraph.Build))]
    public class LayoutGraphBuildOverridePatch
    {
        [HarmonyPostfix]
        public static void Postfix(LayoutBlueprint __result)
        {
            if (ConstructLayoutOverridePatch.Constructing)
            {
                ConstructLayoutOverridePatch.Stretch(__result);
            }
        }
    }
}
