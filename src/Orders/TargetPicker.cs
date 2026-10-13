using HighCommand.Army;
using HighCommand.Mode;
using RoR2;
using UnityEngine;

namespace HighCommand.Orders
{
    /// <summary>
    /// Chooses the enemy one hero fights (server; one instance per hero, it remembers the targets the hero failed to reach).
    ///
    /// Every candidate gets a cost in meters, lower is better: the distance (climbing counts extra for a melee hero, a
    /// flier out of its reach a lot extra), minus bonuses for what matters in a fight: the monster hitting this hero or
    /// a friend, the commander's mark, Mending elites (healers), bosses, elites, hard hitters, the wounded, the enemy the
    /// friends already fire at (focus fire) and the current target (no flip-flopping between two equal ones).
    /// No allocations; line of sight is tested lazily, only for the best few that need it.
    /// </summary>
    internal sealed class TargetPicker
    {
        internal struct Query
        {
            internal Vector3 Center;      // enemies within Radius of this point are candidates ...
            internal float Radius;
            internal float AssistRadius;  // ... and those fighting the army within this distance of the hero (0 = none)
            internal Tether Tether;       // the hero fights from inside this circle
            internal bool RequireSight;   // an enemy that fights nobody of the army must be in sight to be picked
            internal bool StrictSight;    // every candidate must be in sight (a hero that cannot walk to its target)
            internal bool OnlyAttackers;  // passive stance: only enemies that fight this very hero
        }

        // ---- costs, in meters of walking -------------------------------------------------------------------
        private const float MeleeClimbFree = 2.5f;    // height difference a melee hero handles without a detour
        private const float MeleeClimbCost = 3f;      // per meter above that
        private const float MeleeReachUp = 4f;        // a flier higher than this above a melee hero is out of its reach
        private const float FlierOutOfReach = 25f;
        private const float RangedClimbCost = 0.3f;
        private const float ThreatToSelf = 10f;
        private const float ThreatToFriend = 5f;
        private const float MarkBonus = 25f;
        private const float HealerBonus = 8f;
        private const float BigBonus = 6f;
        private const float EliteBonus = 3f;
        private const float DangerScale = 40f;        // the monster's damage per hit as a share of the hero's health ...
        private const float DangerCap = 8f;           // ... and the most that counts
        private const float ShooterBonus = 2f;        // ranged heroes prefer to answer ranged monsters
        private const float WoundedBonus = 8f;
        private const float FocusPerFriend = 3f;
        private const int FocusFriendsCounted = 3;
        private const float Stickiness = 8f;
        private const int MaxSightTests = 4;
        private const int MemorySize = 8;

        private static readonly TeamIndex[] HostileTeams = { TeamIndex.Monster, TeamIndex.Lunar, TeamIndex.Void };

        private struct Candidate
        {
            internal CharacterBody Body;
            internal float Cost;
            internal bool NeedsSight;
        }

        private static Candidate[] candidates = new Candidate[64];
        private static readonly CharacterBody[] FocusBodies = new CharacterBody[32];
        private static readonly int[] FocusCounts = new int[32];

        private readonly CharacterBody[] unreachable = new CharacterBody[MemorySize];
        private readonly float[] unreachableUntil = new float[MemorySize];

        /// <summary>The hero will not pick this enemy again for a while (it could neither reach nor hit it).</summary>
        internal void MarkUnreachable(CharacterBody enemy, float seconds)
        {
            if (!enemy) return;
            int slot = 0;
            float oldest = float.MaxValue;
            for (int i = 0; i < MemorySize; i++)
            {
                if (unreachable[i] == enemy) { slot = i; break; }
                if (unreachableUntil[i] < oldest) { oldest = unreachableUntil[i]; slot = i; }
            }
            unreachable[slot] = enemy;
            unreachableUntil[slot] = Time.time + seconds;
        }

        internal void Forget()
        {
            for (int i = 0; i < MemorySize; i++) { unreachable[i] = null; unreachableUntil[i] = 0f; }
        }

        internal bool IsUnreachable(CharacterBody enemy)
        {
            float now = Time.time;
            for (int i = 0; i < MemorySize; i++)
                if (unreachable[i] == enemy && unreachableUntil[i] > now) return true;
            return false;
        }

        /// <param name="self">the hero's body</param>
        /// <param name="melee">the hero fights at arm's length</param>
        /// <param name="reach">how far the hero's skills reach beyond its tether</param>
        /// <param name="current">the target the hero has now (gets the stickiness bonus)</param>
        internal CharacterBody Pick(CharacterBody self, bool melee, float reach, in Query q, CharacterBody current)
        {
            TeamIndex own = self.teamComponent ? self.teamComponent.teamIndex : TeamIndex.Player;
            Vector3 origin = self.corePosition;
            float selfHealth = Mathf.Max(1f, self.healthComponent ? self.healthComponent.fullCombinedHealth : 1f);
            float radiusSqr = q.Radius * q.Radius;
            float assistSqr = q.AssistRadius * q.AssistRadius;
            int focusUsed = CountFocus(self);
            float focusWeight = AiTuning.FocusFireWeight.Value;

            int n = 0;
            for (int t = 0; t < HostileTeams.Length; t++)
            {
                var members = TeamComponent.GetTeamMembers(HostileTeams[t]);
                for (int i = 0; i < members.Count; i++)
                {
                    CharacterBody b = members[i] ? members[i].body : null;
                    if (!b) continue;
                    Vector3 p = b.corePosition;
                    float fromCenterSqr = (p - q.Center).sqrMagnitude;
                    float fromHeroSqr = (p - origin).sqrMagnitude;
                    if (fromCenterSqr > radiusSqr && fromHeroSqr > assistSqr) continue; // the cheap test first
                    if (!Perception.IsTargetable(b, own) || IsUnreachable(b)) continue;
                    if (q.Tether.Active && q.Tether.Distance(p) > q.Tether.Radius + reach) continue;

                    CharacterBody hunted = EnemyTraits.TargetOf(b);
                    bool fightsMe = hunted == self || (self.healthComponent && self.healthComponent.lastHitAttacker == b.gameObject
                                                        && self.healthComponent.timeSinceLastHit < 3f);
                    bool fightsArmy = fightsMe || (hunted && hunted.teamComponent && hunted.teamComponent.teamIndex == own);
                    if (q.OnlyAttackers && !fightsMe) continue;
                    if (fromCenterSqr > radiusSqr && !fightsArmy) continue; // outside the search: only the army's attackers

                    float cost = Cost(self, b, melee, origin, selfHealth);
                    if (fightsMe) cost -= ThreatToSelf;
                    else if (fightsArmy) cost -= ThreatToFriend;
                    cost -= focusWeight * FocusPerFriend * Mathf.Min(FocusFriendsCounted, FocusOn(b, focusUsed));
                    if (b == current) cost -= Stickiness;

                    if (n == candidates.Length) System.Array.Resize(ref candidates, n * 2);
                    candidates[n].Body = b;
                    candidates[n].Cost = cost;
                    candidates[n].NeedsSight = q.StrictSight || (q.RequireSight && !fightsArmy && b != current);
                    n++;
                }
            }
            if (n == 0) return null;

            // the best that passes the sight test; at most MaxSightTests rays
            Vector3 eye = self.inputBank ? self.inputBank.aimOrigin : origin;
            int tests = 0;
            for (int round = 0; round < n; round++)
            {
                int best = -1;
                float bestCost = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    if (candidates[i].Body && candidates[i].Cost < bestCost) { bestCost = candidates[i].Cost; best = i; }
                }
                if (best < 0) break;
                CharacterBody body = candidates[best].Body;
                candidates[best].Body = null; // taken out of the next rounds
                if (!candidates[best].NeedsSight) return Clear(n, body);
                if (tests++ >= MaxSightTests) break;
                if (Perception.HasLineOfSight(eye, body.corePosition)) return Clear(n, body);
            }
            return Clear(n, null);
        }

        private static CharacterBody Clear(int n, CharacterBody result)
        {
            for (int i = 0; i < n; i++) candidates[i].Body = null; // no references kept to dead monsters
            return result;
        }

        private static float Cost(CharacterBody self, CharacterBody b, bool melee, Vector3 origin, float selfHealth)
        {
            Vector3 d = b.corePosition - origin;
            float dy = d.y;
            d.y = 0f;
            float cost = d.magnitude;
            if (melee)
            {
                float climb = Mathf.Abs(dy) - MeleeClimbFree;
                if (climb > 0f) cost += climb * MeleeClimbCost;
                if (b.isFlying && dy > MeleeReachUp) cost += FlierOutOfReach;
            }
            else
            {
                cost += Mathf.Abs(dy) * RangedClimbCost;
                if (EnemyTraits.IsRanged(b)) cost -= ShooterBonus;
            }

            if (EnemyTraits.IsMarked(b)) cost -= MarkBonus;
            if (EnemyTraits.IsHealer(b)) cost -= HealerBonus;
            if (EnemyTraits.IsBig(b)) cost -= BigBonus;
            if (b.isElite) cost -= EliteBonus;
            cost -= Mathf.Min(DangerCap, b.damage / selfHealth * DangerScale);

            HealthComponent hc = b.healthComponent;
            if (hc && hc.fullCombinedHealth > 0f) cost -= (1f - hc.combinedHealthFraction) * WoundedBonus;
            return cost;
        }

        // Which enemies the other heroes fight right now, and how many of them each.
        private static int CountFocus(CharacterBody self)
        {
            HighCommandRun run = HighCommandRun.Instance;
            if (run == null) return 0;
            int used = 0;
            var heroes = run.Army.Heroes;
            for (int h = 0; h < heroes.Count; h++)
            {
                HeroBrain brain = heroes[h].Brain;
                CharacterBody enemy = brain ? brain.Enemy : null;
                if (!enemy || brain.Mode != BrainMode.Fight || heroes[h].Body == self) continue;
                int at = -1;
                for (int i = 0; i < used; i++) if (FocusBodies[i] == enemy) { at = i; break; }
                if (at < 0)
                {
                    if (used == FocusBodies.Length) continue;
                    at = used++;
                    FocusBodies[at] = enemy;
                    FocusCounts[at] = 0;
                }
                FocusCounts[at]++;
            }
            return used;
        }

        private static int FocusOn(CharacterBody enemy, int used)
        {
            for (int i = 0; i < used; i++) if (FocusBodies[i] == enemy) return FocusCounts[i];
            return 0;
        }
    }
}
