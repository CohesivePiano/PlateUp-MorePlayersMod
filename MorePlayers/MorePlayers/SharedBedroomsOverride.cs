using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kitchen;
using KitchenData;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace MorePlayers
{
    // The HQ has 4 bedrooms, one per player index 0-3. Each bedroom's furniture (bed, outfit station,
    // occupation indicator) is owned by the player with that index, and only the owner can use it, so
    // players 5-8 had no way to change outfit or colour and spawned at the world origin.
    // Turn each bedroom into a shared room: player i (4-7) gets a second set of furniture and a spawn
    // point in bedroom i - 4. Free tiles are found from the HQ's layout at runtime; a placement is only
    // accepted if every piece of furniture in the room stays reachable and the room stays walkable.
    [HarmonyPatch(typeof(CreateBedrooms), "OnUpdate")]
    public class SharedBedroomsOverridePatch
    {
        private const int VanillaBedrooms = 4;

        private static readonly MethodInfo CreateAssigned = AccessTools.Method(typeof(CreateBedrooms), "CreateAssigned");
        private static readonly MethodInfo PlaceSpawnMarker = AccessTools.Method(typeof(CreateBedrooms), "PlaceSpawnMarker");

        private struct Tile
        {
            public Vector2Int Pos;
            public Reachability Reach;
        }

        private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        [HarmonyPostfix]
        public static void Postfix(CreateBedrooms __instance)
        {
            int max = Config.ConfigHelper.getMaxPlayers();
            if (max <= VanillaBedrooms || !Config.ConfigHelper.getSharedBedrooms())
            {
                return;
            }
            EntityManager em = __instance.EntityManager;
            Dictionary<Vector2Int, Tile> layout = ReadLayout(em, out Dictionary<Vector2Int, int> room_of);
            if (layout == null)
            {
                MorePlayers.Log.LogWarning("Shared bedrooms: HQ layout not found, skipping");
                return;
            }
            HashSet<Vector2Int> occupied = ReadOccupied(em, out HashSet<Vector2Int> spawns);

            for (int index = VanillaBedrooms; index < max; index++)
            {
                int bedroom = (index - VanillaBedrooms) % VanillaBedrooms;
                Vector2Int anchor = ToTile(LobbyPositionAnchors.Bedrooms[bedroom]);
                if (!room_of.TryGetValue(anchor, out int room_id))
                {
                    MorePlayers.Log.LogWarning($"Shared bedrooms: bedroom {bedroom} not found in HQ layout");
                    continue;
                }
                List<Vector2Int> room = room_of.Where(r => r.Value == room_id).Select(r => r.Key).ToList();
                if (!TryPlan(layout, room, occupied, spawns, anchor, out Plan plan))
                {
                    MorePlayers.Log.LogWarning($"Shared bedrooms: no free space for player {index + 1} in bedroom {bedroom + 1}");
                    continue;
                }
                Build(__instance, em, index, plan);
                foreach (Vector2Int t in plan.Furniture)
                {
                    occupied.Add(t);
                }
                spawns.Add(plan.Spawn);
                MorePlayers.Log.LogInfo($"Shared bedrooms: player {index + 1} shares bedroom {bedroom + 1} ({plan.Describe()})");
            }
        }

        private class Plan
        {
            public Vector2Int? Bed;
            public Vector2Int? BedProxy;
            public Vector2Int? Outfit;
            public Vector2Int? Indicator;
            public Vector2Int Spawn;

            public IEnumerable<Vector2Int> Furniture => new[] { Bed, BedProxy, Outfit, Indicator }.Where(t => t.HasValue).Select(t => t.Value);

            public string Describe()
            {
                return $"bed {(Bed.HasValue ? "yes" : "no")}, outfit {(Outfit.HasValue ? "yes" : "no")}, indicator {(Indicator.HasValue ? "yes" : "no")}";
            }
        }

        private static void Build(CreateBedrooms system, EntityManager em, int index, Plan plan)
        {
            if (plan.Bed.HasValue)
            {
                // Same arrangement as vanilla: the bed sits behind an interaction proxy, facing away from it.
                Vector3 facing = ToWorld(plan.Bed.Value) - ToWorld(plan.BedProxy.Value);
                Entity bed = Create(system, index, AssetReference.Bed, plan.Bed.Value, facing);
                Entity proxy = Create(system, index, AssetReference.InteractionProxy, plan.BedProxy.Value, facing);
                em.AddComponentData(proxy, new CInteractionProxy { Target = bed, IsActive = true });
            }
            if (plan.Outfit.HasValue)
            {
                Entity outfit = Create(system, index, AssetReference.OutfitStation, plan.Outfit.Value, Vector3.forward);
                em.AddComponent<CCosmeticSelector>(outfit);
                em.SetComponentData(outfit, new CCosmeticSelector { Type = CosmeticType.Outfit, DrawLocation = new Vector3(-0.25f, 0f, 0f) });
            }
            if (plan.Indicator.HasValue)
            {
                Create(system, index, AssetReference.OccupationIndicator, plan.Indicator.Value, Vector3.forward);
            }
            PlaceSpawnMarker.Invoke(system, new object[] { index, ToWorld(plan.Spawn) });
        }

        private static Entity Create(CreateBedrooms system, int index, int appliance_id, Vector2Int tile, Vector3 facing)
        {
            Appliance appliance = GameData.Main.Get<Appliance>(appliance_id);
            return (Entity)CreateAssigned.Invoke(system, new object[] { index, appliance, ToWorld(tile), facing });
        }

        // Searches for the fullest set of furniture that fits: bed + outfit + indicator, then without the bed,
        // then just a spawn point. Candidates are tried nearest the vanilla bed first.
        private static bool TryPlan(Dictionary<Vector2Int, Tile> layout, List<Vector2Int> room, HashSet<Vector2Int> occupied, HashSet<Vector2Int> spawns, Vector2Int anchor, out Plan plan)
        {
            HashSet<Vector2Int> room_set = new HashSet<Vector2Int>(room);
            // Furniture can't go on existing furniture or on anyone's spawn point. Keep the search small.
            List<Vector2Int> free = room.Where(t => !occupied.Contains(t) && !spawns.Contains(t))
                .OrderBy(t => (t - anchor).sqrMagnitude).Take(16).ToList();
            List<Vector2Int> existing = room.Where(occupied.Contains).ToList();

            List<(Vector2Int bed, Vector2Int proxy)> bed_pairs = new List<(Vector2Int, Vector2Int)>();
            foreach (Vector2Int proxy in free)
            {
                foreach (Vector2Int dir in Directions)
                {
                    Vector2Int bed = proxy + dir;
                    if (free.Contains(bed) && CanStep(layout, proxy, bed))
                    {
                        bed_pairs.Add((bed, proxy));
                    }
                }
            }

            foreach (bool with_bed in new[] { true, false })
            {
                IEnumerable<(Vector2Int bed, Vector2Int proxy)?> beds = with_bed
                    ? bed_pairs.Select(p => ((Vector2Int, Vector2Int)?)p)
                    : new (Vector2Int, Vector2Int)?[] { null };
                foreach (var pair in beds)
                {
                    foreach (Vector2Int outfit in free)
                    {
                        foreach (Vector2Int indicator in free)
                        {
                            Plan candidate = new Plan
                            {
                                Bed = pair?.bed,
                                BedProxy = pair?.proxy,
                                Outfit = outfit,
                                Indicator = indicator
                            };
                            List<Vector2Int> furniture = candidate.Furniture.ToList();
                            if (furniture.Distinct().Count() != furniture.Count)
                            {
                                continue;
                            }
                            if (TryReach(layout, room_set, existing, furniture, out HashSet<Vector2Int> reached)
                                && TryPickSpawn(reached, spawns, candidate.BedProxy ?? outfit, out candidate.Spawn))
                            {
                                plan = candidate;
                                return true;
                            }
                        }
                    }
                }
            }

            // No room for furniture: still give the player a spawn point inside the bedroom.
            if (TryReach(layout, room_set, existing, new List<Vector2Int>(), out HashSet<Vector2Int> open)
                && TryPickSpawn(open, spawns, anchor, out Vector2Int only_spawn))
            {
                plan = new Plan { Spawn = only_spawn };
                return true;
            }
            plan = null;
            return false;
        }

        // A free floor tile near the new furniture that isn't someone else's spawn point.
        private static bool TryPickSpawn(HashSet<Vector2Int> reached, HashSet<Vector2Int> spawns, Vector2Int near, out Vector2Int spawn)
        {
            List<Vector2Int> options = reached.Where(t => !spawns.Contains(t)).OrderBy(t => (t - near).sqrMagnitude).ToList();
            spawn = options.FirstOrDefault();
            return options.Count > 0;
        }

        // Succeeds if the walkable tiles of the room form one connected area that touches the room's exit
        // and is next to every piece of furniture (old and new), so everything can still be used.
        // Spawn points are walkable.
        private static bool TryReach(Dictionary<Vector2Int, Tile> layout, HashSet<Vector2Int> room, List<Vector2Int> existing, List<Vector2Int> added, out HashSet<Vector2Int> reached)
        {
            reached = null;
            HashSet<Vector2Int> blocked = new HashSet<Vector2Int>(existing.Concat(added));
            HashSet<Vector2Int> walkable = new HashSet<Vector2Int>(room.Where(t => !blocked.Contains(t)));

            List<Vector2Int> exits = walkable.Where(t => Directions.Any(d => !room.Contains(t + d) && CanStep(layout, t, t + d))).ToList();
            if (exits.Count == 0)
            {
                return false;
            }
            reached = new HashSet<Vector2Int> { exits[0] };
            Queue<Vector2Int> queue = new Queue<Vector2Int>(reached);
            while (queue.Count > 0)
            {
                Vector2Int t = queue.Dequeue();
                foreach (Vector2Int d in Directions)
                {
                    Vector2Int n = t + d;
                    if (walkable.Contains(n) && !reached.Contains(n) && CanStep(layout, t, n))
                    {
                        reached.Add(n);
                        queue.Enqueue(n);
                    }
                }
            }
            if (reached.Count != walkable.Count)
            {
                return false;
            }
            HashSet<Vector2Int> area = reached;
            return blocked.All(f => Directions.Any(d => area.Contains(f + d) && CanStep(layout, f + d, f)));
        }

        private static bool CanStep(Dictionary<Vector2Int, Tile> layout, Vector2Int from, Vector2Int to)
        {
            if (!layout.TryGetValue(from, out Tile tile) || !layout.ContainsKey(to))
            {
                return false;
            }
            Vector2Int d = to - from;
            return tile.Reach[d.x, d.y];
        }

        private static Dictionary<Vector2Int, Tile> ReadLayout(EntityManager em, out Dictionary<Vector2Int, int> room_of)
        {
            room_of = new Dictionary<Vector2Int, int>();
            using (EntityQuery query = em.CreateEntityQuery(typeof(SLayout), typeof(CLayoutRoomTile)))
            {
                if (query.CalculateEntityCount() == 0)
                {
                    return null;
                }
                using (NativeArray<Entity> layouts = query.ToEntityArray(Allocator.Temp))
                {
                    Dictionary<Vector2Int, Tile> tiles = new Dictionary<Vector2Int, Tile>();
                    foreach (CLayoutRoomTile t in em.GetBuffer<CLayoutRoomTile>(layouts[0]))
                    {
                        Vector2Int pos = ToTile(t.Position);
                        tiles[pos] = new Tile { Pos = pos, Reach = t.Reachability };
                        room_of[pos] = t.RoomID;
                    }
                    return tiles;
                }
            }
        }

        // Tiles with something already placed on them in the HQ (vanilla bedroom furniture etc.),
        // plus existing spawn points, which stay walkable but must not get furniture.
        private static HashSet<Vector2Int> ReadOccupied(EntityManager em, out HashSet<Vector2Int> spawn_tiles)
        {
            spawn_tiles = new HashSet<Vector2Int>();
            HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
            using (EntityQuery query = em.CreateEntityQuery(ComponentType.ReadOnly<CPosition>(), ComponentType.Exclude<CPlayer>()))
            using (NativeArray<CPosition> positions = query.ToComponentDataArray<CPosition>(Allocator.Temp))
            {
                foreach (CPosition p in positions)
                {
                    // Some pieces sit between tiles (e.g. z + 0.5); block every tile they touch.
                    Vector3 v = p.Position;
                    for (int x = Mathf.FloorToInt(v.x + 0.01f); x <= Mathf.CeilToInt(v.x - 0.01f); x++)
                    {
                        for (int z = Mathf.FloorToInt(v.z + 0.01f); z <= Mathf.CeilToInt(v.z - 0.01f); z++)
                        {
                            occupied.Add(new Vector2Int(x, z));
                        }
                    }
                }
            }
            using (EntityQuery query = em.CreateEntityQuery(ComponentType.ReadOnly<CPlayerSpawnLocation>()))
            using (NativeArray<CPlayerSpawnLocation> spawns = query.ToComponentDataArray<CPlayerSpawnLocation>(Allocator.Temp))
            {
                foreach (CPlayerSpawnLocation s in spawns)
                {
                    spawn_tiles.Add(ToTile(s.Location));
                }
            }
            return occupied;
        }

        private static Vector2Int ToTile(Vector3 v)
        {
            return new Vector2Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.z));
        }

        private static Vector3 ToWorld(Vector2Int t)
        {
            return new Vector3(t.x, 0f, t.y);
        }
    }
}
