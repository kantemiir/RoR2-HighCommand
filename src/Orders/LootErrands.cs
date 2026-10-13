using System.Collections.Generic;
using HighCommand.Army;
using HighCommand.Mode;
using RoR2;
using UnityEngine;

namespace HighCommand.Orders
{
    /// <summary>
    /// A hero's own trips to loot (server; one per hero): a barrel or an affordable chest near its post when it has
    /// nothing else to do, and the chests of the whole map for the "seek chests" order. Only chests - shops, shrines,
    /// printers and drones are the player's choice. The gold other heroes are already walking to spend is kept for them,
    /// and an object another hero is going to is left to it.
    /// </summary>
    internal sealed class LootErrands
    {
        private const float ScanInterval = 1f;
        private const float SeekScanInterval = 0.5f;
        private const float BarrelFromPost = 22f, BarrelFromHero = 28f;
        private const float ChestFromPost = 30f, ChestFromHero = 35f;
        private const float SameLevel = 6f;          // loot farther above or below is another level of the map
        private const float LongWayFactor = 1.6f;    // the walk to a chest is about this much longer than the straight line
        private const float WalkSpeed = 4f;          // m/s counted with fights and detours
        private const float MinBudget = 45f, MaxBudget = 300f, BaseBudget = 30f;

        private readonly HeroBrain brain;
        private readonly HashSet<GameObject> skip = new HashSet<GameObject>();
        private float barrelScanAt, chestScanAt;

        internal LootErrands(HeroBrain brain) => this.brain = brain;

        internal void Reset()
        {
            skip.Clear();
            barrelScanAt = chestScanAt = 0f;
        }

        /// <summary>An object the hero could not use from here: it is not tried again on this stage.</summary>
        internal void Skip(GameObject target)
        {
            if (target) skip.Add(target);
        }

        /// <summary>
        /// A hero with nothing to do opens a barrel (free gold), or a chest the purse can pay for, near its post. Only in
        /// calm: <paramref name="calm"/> = no enemy near, not hit lately, no zone to hold. True when a trip was planned
        /// (it starts at the next think).
        /// </summary>
        internal bool TryNearbyTrip(Vector3 pos, bool calm)
        {
            if (!calm) return false;
            return TryBarrel(pos) || TryChest(pos);
        }

        private bool TryBarrel(Vector3 pos)
        {
            if (!Settings.AutoOpenBarrels.Value || Time.time < barrelScanAt) return false;
            barrelScanAt = Time.time + ScanInterval;

            Vector3 post = brain.Anchor;
            BarrelInteraction best = null;
            float bestDist = float.MaxValue;
            var barrels = InstanceTracker.GetInstancesList<BarrelInteraction>();
            for (int i = 0; i < barrels.Count; i++)
            {
                BarrelInteraction b = barrels[i];
                if (!b || b.Networkopened || skip.Contains(b.gameObject)) continue;
                Vector3 bp = b.transform.position;
                if ((bp - post).sqrMagnitude > BarrelFromPost * BarrelFromPost || Mathf.Abs(bp.y - pos.y) > SameLevel) continue;
                float d = (bp - pos).sqrMagnitude;
                if (d > BarrelFromHero * BarrelFromHero || d >= bestDist || ClaimedByAnother(b.gameObject)) continue;
                bestDist = d;
                best = b;
            }
            if (!best) return false;

            brain.Errand(new Order { Kind = OrderKind.Interact, Target = best.gameObject, Point = best.transform.position, auto = true }, queued: false);
            if (Settings.VerboseLog.Value) Plugin.Log.LogInfo($"{brain.Hero}: opening a barrel at {best.transform.position:0}");
            return true;
        }

        private bool TryChest(Vector3 pos)
        {
            if (!Settings.AutoOpenChests.Value || Time.time < chestScanAt) return false;
            chestScanAt = Time.time + ScanInterval;

            Vector3 post = brain.Anchor;
            long spare = (long)Economy.Gold - GoldClaimedByOthers();
            PurchaseInteraction best = null;
            float bestDist = float.MaxValue;
            var all = InstanceTracker.GetInstancesList<PurchaseInteraction>();
            for (int i = 0; i < all.Count; i++)
            {
                PurchaseInteraction p = all[i];
                if (!Buyable(p, spare)) continue;
                Vector3 cp = p.transform.position;
                if ((cp - post).sqrMagnitude > ChestFromPost * ChestFromPost || Mathf.Abs(cp.y - pos.y) > SameLevel) continue;
                float d = (cp - pos).sqrMagnitude;
                if (d > ChestFromHero * ChestFromHero || d >= bestDist || ClaimedByAnother(p.gameObject)) continue;
                bestDist = d;
                best = p;
            }
            if (!best) return false;

            brain.Errand(new Order { Kind = OrderKind.Interact, Target = best.gameObject, Point = best.transform.position, auto = true }, queued: false);
            if (Settings.VerboseLog.Value) Plugin.Log.LogInfo($"{brain.Hero}: opening a chest (${best.cost}) at {best.transform.position:0}");
            return true;
        }

        /// <summary>
        /// The "seek chests" order: plans a trip to the nearest chest of the map the purse can pay for (another one per
        /// hero). False when none is left; <paramref name="waiting"/> = scanned too recently, nothing to say yet.
        /// </summary>
        internal bool PlanSeekTrip(Vector3 pos, out bool waiting, out float distance)
        {
            distance = 0f;
            waiting = Time.time < chestScanAt;
            if (waiting) return true;
            chestScanAt = Time.time + SeekScanInterval;

            PurchaseInteraction chest = NearestChest(pos, out int _);
            if (!chest) return false;
            distance = Vector3.Distance(chest.transform.position, pos);
            float budget = Mathf.Clamp(BaseBudget + distance * LongWayFactor / WalkSpeed, MinBudget, MaxBudget);
            brain.Errand(new Order { Kind = OrderKind.Interact, Target = chest.gameObject, Point = chest.transform.position, auto = true, walkBudget = budget },
                queued: false);
            if (Settings.VerboseLog.Value) Plugin.Log.LogInfo($"{brain.Hero}: seeking a chest (${chest.cost}) at {chest.transform.position:0}");
            return true;
        }

        /// <summary>Chests the hero could open now: affordable (minus the gold others are on the way to spend), not taken.</summary>
        internal int CountChests(Vector3 pos)
        {
            NearestChest(pos, out int count);
            return count;
        }

        private PurchaseInteraction NearestChest(Vector3 pos, out int count)
        {
            PurchaseInteraction best = null;
            count = 0;
            long spare = (long)Economy.Gold - GoldClaimedByOthers();
            float bestSqr = float.MaxValue;
            var all = InstanceTracker.GetInstancesList<PurchaseInteraction>();
            for (int i = 0; i < all.Count; i++)
            {
                PurchaseInteraction p = all[i];
                if (!Buyable(p, spare) || ClaimedByAnother(p.gameObject)) continue;
                count++;
                Vector3 d = p.transform.position - pos;
                d.y *= 2f; // a chest on a ledge far above is a long way round
                float sqr = d.sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = p; }
            }
            return best;
        }

        private bool Buyable(PurchaseInteraction p, long spare) =>
            p && p.available && p.costType == CostTypeIndex.Money && p.cost <= spare && !skip.Contains(p.gameObject)
            && Interactables.KindOf(p.gameObject) == InteractKind.Chest;

        /// <summary>The gold of the chests and shops other heroes are on their way to (it will be spent there).</summary>
        private long GoldClaimedByOthers()
        {
            HighCommandRun run = HighCommandRun.Instance;
            if (run == null) return 0;
            long sum = 0;
            var heroes = run.Army.Heroes;
            for (int k = 0; k < heroes.Count; k++)
            {
                HeroBrain other = heroes[k].Brain;
                if (!other || other == brain) continue;
                if (other.Current != null) sum += GoldOf(other.Current);
                var queue = other.Queue;
                for (int i = 0; i < queue.Count; i++) sum += GoldOf(queue[i]);
            }
            return sum;
        }

        private static long GoldOf(Order o)
        {
            if (o.Kind != OrderKind.Interact || !o.Target) return 0;
            return Interactables.TryGetPrice(o.Target, out CostTypeIndex type, out int cost) && type == CostTypeIndex.Money ? cost : 0;
        }

        private bool ClaimedByAnother(GameObject target)
        {
            HighCommandRun run = HighCommandRun.Instance;
            if (run == null) return false;
            var heroes = run.Army.Heroes;
            for (int k = 0; k < heroes.Count; k++)
            {
                HeroBrain other = heroes[k].Brain;
                if (!other || other == brain) continue;
                if (other.Current != null && other.Current.Target == target) return true;
                var queue = other.Queue;
                for (int i = 0; i < queue.Count; i++) if (queue[i].Target == target) return true;
            }
            return false;
        }
    }
}
