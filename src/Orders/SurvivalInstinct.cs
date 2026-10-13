using System.Collections.Generic;
using HighCommand.Army;
using HighCommand.Mode;
using RoR2;
using RoR2.Navigation;
using UnityEngine;

namespace HighCommand.Orders
{
    /// <summary>
    /// When a hurt hero runs from a fight, and where to (server; one per hero).
    ///
    /// A hero runs only from a real threat: its health is below the profile's retreat line AND it is being hit or a
    /// monster close by is after it. It stops as soon as that threat is gone (no hits for a moment, nobody close on its
    /// heels) or it has healed past the recover line, so it goes back into the fight instead of hiding until healed.
    /// A hero that has run for the longest allowed time fights on and is not allowed to run again for a while: running
    /// for ever from monsters that follow it gets nobody anywhere.
    /// </summary>
    internal sealed class SurvivalInstinct
    {
        private const float HitMemory = 2.5f;        // hit this recently = under fire
        private const float DangerRadius = 15f;      // a monster this close and after the hero is a threat
        private const float MinFleeSeconds = 1.5f;   // no flickering between fleeing and fighting
        private const float GoalRefresh = 1.5f;      // a new place to run to only this often (zig-zag otherwise)
        private const float GoalReached = 4f;
        private const float FriendSearch = 120f;
        private const float FriendHealthy = 0.4f;
        private const float NodeMin = 10f, NodeMax = 34f;
        private const float LevelBelow = 5f, LevelAbove = 8f; // never down a cliff, never up a wall
        private const int NodeSamples = 120;
        private const float NodeClimbPenalty = 1.5f; // per meter of height between the hero and a node
        private const float NodeRallyPull = 0.15f;   // per meter from the node to the rally point
        private const float ThreatRange = 40f;       // monsters this close make up the threat to run from
        private const float FallbackRun = 20f;       // no node graph: run this far straight away

        private static readonly TeamIndex[] HostileTeams = { TeamIndex.Monster, TeamIndex.Lunar, TeamIndex.Void };
        private static readonly List<NodeGraph.NodeIndex> NodeBuffer = new List<NodeGraph.NodeIndex>(256);

        internal bool Fleeing { get; private set; }

        private float since;
        private float cooldownUntil;
        private Vector3 goal;
        private float goalAt = -100f;

        internal void Reset()
        {
            Fleeing = false;
            cooldownUntil = 0f;
            goalAt = -100f;
        }

        /// <param name="allowed">the current order lets the hero run (an explicit move, hold or use is never interrupted)</param>
        /// <returns>true while the hero should run</returns>
        internal bool Update(Hero hero, CharacterBody self, HeroProfile profile, bool allowed)
        {
            HealthComponent hc = self.healthComponent;
            if (!Settings.AutoRetreat.Value || !allowed || !hc)
            {
                Fleeing = false;
                return false;
            }

            float health = hc.combinedHealthFraction;
            float now = Time.time;
            if (!Fleeing)
            {
                if (now < cooldownUntil || health >= profile.retreatHealth || !Threatened(self, hc)) return false;
                Fleeing = true;
                since = now;
                goalAt = -100f;
                if (Settings.VerboseLog.Value) Plugin.Log.LogInfo($"{hero}: hurt ({health:0.00}) and under attack, running");
                return true;
            }

            bool timedOut = now - since > AiTuning.FleeMaxSeconds.Value;
            bool settled = now - since > MinFleeSeconds && !Threatened(self, hc);
            if (health >= profile.recoverHealth || settled || timedOut)
            {
                Fleeing = false;
                if (timedOut) cooldownUntil = now + AiTuning.FleeCooldownSeconds.Value;
                if (Settings.VerboseLog.Value)
                    Plugin.Log.LogInfo($"{hero}: back to the fight ({(timedOut ? "ran long enough" : settled ? "out of danger" : "healed")}, {health:0.00})");
            }
            return Fleeing;
        }

        private static bool Threatened(CharacterBody self, HealthComponent hc)
        {
            if (hc.timeSinceLastHit < HitMemory) return true;
            Vector3 pos = self.corePosition;
            float r2 = DangerRadius * DangerRadius;
            for (int t = 0; t < HostileTeams.Length; t++)
            {
                var members = TeamComponent.GetTeamMembers(HostileTeams[t]);
                for (int i = 0; i < members.Count; i++)
                {
                    CharacterBody b = members[i] ? members[i].body : null;
                    if (!b || (b.corePosition - pos).sqrMagnitude > r2) continue;
                    if (EnemyTraits.TargetOf(b) == self && Perception.IsTargetable(b, self.teamComponent.teamIndex)) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Where to run: chosen again only every <see cref="GoalRefresh"/> s or when reached, so the hero does not zig-zag.
        /// </summary>
        internal Vector3 Goal(Hero hero, Vector3 pos)
        {
            Vector3 left = goal - pos;
            left.y = 0f;
            if (Time.time - goalAt < GoalRefresh && left.sqrMagnitude > GoalReached * GoalReached) return goal;
            goalAt = Time.time;
            goal = ChooseGoal(hero, pos);
            if (Settings.VerboseLog.Value) Plugin.Log.LogInfo($"{hero}: running to {goal:0} (from {pos:0})");
            return goal;
        }

        // A healthy friend who is not itself in the thick of it; alone: a ground node on about the same level, away from
        // the enemies and towards the rally point. A point far below (the foot of a cliff) is never chosen: a hero
        // walking there falls and lands at 1 HP among the enemies.
        private static Vector3 ChooseGoal(Hero hero, Vector3 pos)
        {
            HighCommandRun run = HighCommandRun.Instance;
            Vector3 threat = ThreatCentre(pos, out int threats);

            Hero friend = null;
            float friendCost = FriendSearch * FriendSearch;
            if (run != null)
            {
                var heroes = run.Army.Heroes;
                for (int k = 0; k < heroes.Count; k++)
                {
                    Hero h = heroes[k];
                    if (h == hero || !h.IsAlive) continue;
                    CharacterBody b = h.Body;
                    HealthComponent hc = b ? b.healthComponent : null;
                    if (!hc || hc.combinedHealthFraction < FriendHealthy) continue;
                    Vector3 fp = b.footPosition;
                    if (Mathf.Abs(fp.y - pos.y) > LevelAbove) continue;
                    float cost = (fp - pos).sqrMagnitude;
                    // a friend standing right in the middle of the monsters is no shelter
                    if (threats > 0 && (fp - threat).sqrMagnitude < (pos - threat).sqrMagnitude) cost *= 4f;
                    if (cost < friendCost) { friendCost = cost; friend = h; }
                }
            }
            if (friend != null) return friend.Body.footPosition;

            Vector3 rally = run != null ? run.RallyPoint : pos;
            if (threats == 0) return rally;

            SceneInfo info = SceneInfo.instance;
            NodeGraph graph = info ? info.groundNodes : null;
            if (graph)
            {
                NodeBuffer.Clear();
                graph.FindNodesInRange(pos, NodeMin, NodeMax, HullMask.Human, NodeBuffer);
                int step = Mathf.Max(1, NodeBuffer.Count / NodeSamples); // a big open map has hundreds: a sample is enough
                bool found = false;
                float bestScore = float.MinValue;
                Vector3 best = pos;
                for (int i = 0; i < NodeBuffer.Count; i += step)
                {
                    if (!graph.GetNodePosition(NodeBuffer[i], out Vector3 np)) continue;
                    float dy = np.y - pos.y;
                    if (dy < -LevelBelow || dy > LevelAbove) continue;
                    Vector3 fromThreat = np - threat;
                    fromThreat.y = 0f;
                    Vector3 toRally = rally - np;
                    toRally.y = 0f;
                    float score = fromThreat.magnitude - NodeClimbPenalty * Mathf.Abs(dy) - NodeRallyPull * toRally.magnitude;
                    if (score > bestScore) { bestScore = score; best = np; found = true; }
                }
                NodeBuffer.Clear();
                if (found) return best;
            }

            // no node graph (or nothing on this level): straight away, but only if the ground there is not far below
            Vector3 away = pos - threat;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.back;
            Vector3 p = Formation.SnapToGround(pos + away.normalized * FallbackRun);
            return p.y - pos.y < -LevelBelow ? pos : p;
        }

        private static Vector3 ThreatCentre(Vector3 pos, out int count)
        {
            Vector3 sum = Vector3.zero;
            count = 0;
            for (int t = 0; t < HostileTeams.Length; t++)
            {
                var members = TeamComponent.GetTeamMembers(HostileTeams[t]);
                for (int i = 0; i < members.Count; i++)
                {
                    CharacterBody b = members[i] ? members[i].body : null;
                    if (!b || (b.corePosition - pos).sqrMagnitude > ThreatRange * ThreatRange) continue;
                    sum += b.corePosition;
                    count++;
                }
            }
            return count > 0 ? sum / count : pos;
        }
    }
}
