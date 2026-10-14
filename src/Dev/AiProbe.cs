using System.Collections.Generic;
using System.Text;
using HighCommand.Army;
using HighCommand.Mode;
using HighCommand.Orders;
using RoR2;
using RoR2.CharacterAI;
using UnityEngine;
using UnityEngine.Networking;

namespace HighCommand.Dev
{
    /// <summary>
    /// Measures how the heroes behave, for tuning and the AI test scenarios (host only): time per brain mode, time spent
    /// not attacking while a seen enemy is in reach ("passive"), skills used per slot, damage dealt and taken, deaths,
    /// and how orders end (reached or dropped, reaction time, effective speed). It is added to the run only by
    /// <c>hc_ai reset</c>, so normal play pays nothing for it.
    /// </summary>
    internal sealed class AiProbe : MonoBehaviour
    {
        private const float SampleInterval = 0.25f;
        private const float ReachMeters = 30f;      // a seen enemy this close should be under fire
        private const float AttackGraceSeconds = 1.5f; // a skill used this recently still counts as "attacking"
        private const float ReactMeters = 1.5f;     // an order counts as obeyed once the hero has moved this far
        private const float DoneMeters = 5f;        // an order that ends this close to its point was reached
        private const float StandingSpeed = 1.5f;   // m/s: slower than this counts as standing
        private const float HoldSettleSlack = 1f;
        private const float TraceInterval = 1f;

        private sealed class Stats
        {
            internal float alive, idle, fight, go, cast, flee, combat, passive, standing, stalled, holdDrift, zone, inZone;
            internal readonly int[] skills = new int[4];
            internal float dealt, taken;
            internal int deaths, killsAtStart, stuckEpisodes, done, dropped, reacted, equipment;
            internal float reactSum, speedSum;
            internal float lastSkill = -100f;
            internal bool wasAlive, wasStuck, holdSettled;
            internal Order holdOrder;
            internal CharacterBody body;
            internal System.Action<GenericSkill> onSkill;
            internal Order order;
            internal Vector3 orderStart;
            internal bool orderReacted;
        }

        internal static AiProbe Instance { get; private set; }

        private readonly Dictionary<Hero, Stats> stats = new Dictionary<Hero, Stats>();
        private float sampleTimer, traceTimer, startedAt;
        private bool trace;

        internal static AiProbe Ensure()
        {
            HighCommandRun run = HighCommandRun.Instance;
            if (run == null) return null;
            AiProbe probe = run.GetComponent<AiProbe>();
            return probe ? probe : run.gameObject.AddComponent<AiProbe>();
        }

        private void OnEnable()
        {
            Instance = this;
            GlobalEventManager.onServerDamageDealt += OnDamage;
            EquipmentSlot.onServerEquipmentActivated += OnEquipment;
            ResetStats();
        }

        private void OnDisable()
        {
            GlobalEventManager.onServerDamageDealt -= OnDamage;
            EquipmentSlot.onServerEquipmentActivated -= OnEquipment;
            foreach (Stats s in stats.Values) Unhook(s);
            stats.Clear();
            if (Instance == this) Instance = null;
        }

        internal void ResetStats()
        {
            foreach (Stats s in stats.Values) Unhook(s);
            stats.Clear();
            startedAt = Time.time;
        }

        internal void SetTrace(bool on) => trace = on;

        private Stats For(Hero hero)
        {
            if (!stats.TryGetValue(hero, out Stats s))
            {
                s = new Stats { killsAtStart = hero.Kills };
                stats[hero] = s;
            }
            return s;
        }

        private static void Unhook(Stats s)
        {
            if (s.body && s.onSkill != null) s.body.onSkillActivatedServer -= s.onSkill;
            s.body = null;
            s.onSkill = null;
        }

        private void OnDamage(DamageReport report)
        {
            if (report == null) return;
            Hero attacker = HeroMarker.Of(report.attackerMaster) ?? HeroMarker.Of(report.attackerOwnerMaster);
            if (attacker != null) For(attacker).dealt += report.damageDealt;
            Hero victim = HeroMarker.Of(report.victimMaster);
            if (victim != null) For(victim).taken += report.damageDealt;
        }

        private void OnEquipment(EquipmentSlot slot, EquipmentIndex index)
        {
            Hero hero = slot && slot.characterBody ? HeroMarker.Of(slot.characterBody) : null;
            if (hero != null) For(hero).equipment++;
        }

        private void FixedUpdate()
        {
            if (!NetworkServer.active) return;
            HighCommandRun run = HighCommandRun.Instance;
            if (run == null) return;

            sampleTimer -= Time.fixedDeltaTime;
            if (sampleTimer > 0f) return;
            sampleTimer = SampleInterval;

            foreach (Hero hero in run.Army.Heroes) Sample(hero, SampleInterval);

            if (!trace) return;
            traceTimer -= SampleInterval;
            if (traceTimer > 0f) return;
            traceTimer = TraceInterval;
            var sb = new StringBuilder($"hc_ai trace t={Time.time - startedAt:0.0}");
            foreach (Hero hero in run.Army.Heroes) sb.Append("\n  ").Append(TraceLine(hero));
            Plugin.Log.LogInfo(sb.ToString());
        }

        private void Sample(Hero hero, float dt)
        {
            Stats s = For(hero);
            CharacterBody body = hero.Body;
            HeroBrain brain = hero.Brain;
            bool alive = body && body.healthComponent && body.healthComponent.alive;
            if (!alive || !brain)
            {
                if (s.wasAlive) s.deaths++;
                s.wasAlive = false;
                return;
            }
            s.wasAlive = true;

            if (body != s.body)
            {
                Unhook(s);
                s.body = body;
                s.onSkill = skill => OnSkill(s, body, skill);
                body.onSkillActivatedServer += s.onSkill;
            }

            s.alive += dt;
            if (brain.Fleeing) s.flee += dt;
            switch (brain.Mode)
            {
                case BrainMode.Fight: s.fight += dt; break;
                case BrainMode.Go: s.go += dt; break;
                case BrainMode.Cast: s.cast += dt; break;
                default: s.idle += dt; break;
            }

            Vector3 pos = body.footPosition;
            Vector3 velocity = body.characterMotor ? body.characterMotor.velocity : Vector3.zero;
            velocity.y = 0f;
            if (brain.Mode == BrainMode.Go && velocity.sqrMagnitude < 1f) s.stalled += dt;
            if (brain.Stuck && !s.wasStuck) s.stuckEpisodes++;
            s.wasStuck = brain.Stuck;

            if (brain.ZoneRadius > 0f)
            {
                s.zone += dt;
                Vector3 fromZone = pos - brain.ZoneCenter;
                fromZone.y = 0f;
                if (fromZone.magnitude <= brain.ZoneRadius) s.inZone += dt;
            }

            Order order = brain.Current;
            if (order != null && order.Kind == OrderKind.Hold)
            {
                // measured once the hero has stood in its circle: the way back after a leap is not drifting
                Vector3 drift = pos - order.Point;
                drift.y = 0f;
                if (order != s.holdOrder) { s.holdOrder = order; s.holdSettled = false; }
                if (drift.magnitude <= AiTuning.HoldRadius.Value + HoldSettleSlack) s.holdSettled = true;
                if (s.holdSettled) s.holdDrift = Mathf.Max(s.holdDrift, drift.magnitude);
            }
            if (NearestSeenEnemy(body, out float _))
            {
                s.combat += dt;
                bool attacking = Time.time - s.lastSkill < AttackGraceSeconds;
                if (!attacking && !brain.Fleeing && FightAllowed(order))
                {
                    s.passive += dt;
                    // not even closing in: the hero just stands there while the enemy is in sight
                    if (velocity.sqrMagnitude < StandingSpeed * StandingSpeed) s.standing += dt;
                }
            }

            TrackOrder(s, order, pos);
        }

        private static void OnSkill(Stats s, CharacterBody body, GenericSkill skill)
        {
            if (!body || !body.skillLocator) return;
            int slot = (int)body.skillLocator.FindSkillSlot(skill);
            if (slot >= 0 && slot < s.skills.Length) s.skills[slot]++;
            s.lastSkill = Time.time;
        }

        // Orders that leave the hero free to fight what it meets: not fighting a seen enemy then is passivity.
        private static bool FightAllowed(Order order)
        {
            if (order == null) return true;
            switch (order.Kind)
            {
                case OrderKind.Move:
                case OrderKind.Retreat:
                case OrderKind.Cast:
                case OrderKind.Interact:
                    return false;
                default:
                    return true;
            }
        }

        private static bool NearestSeenEnemy(CharacterBody self, out float distance)
        {
            distance = float.MaxValue;
            CharacterBody nearest = null;
            TeamIndex own = self.teamComponent ? self.teamComponent.teamIndex : TeamIndex.Player;
            Vector3 from = self.corePosition;
            for (TeamIndex team = TeamIndex.Neutral; team < TeamIndex.Count; team++)
            {
                if (team == own || team == TeamIndex.Neutral) continue;
                var members = TeamComponent.GetTeamMembers(team);
                for (int i = 0; i < members.Count; i++)
                {
                    CharacterBody b = members[i] ? members[i].body : null;
                    if (!b || !Perception.IsTargetable(b, own)) continue;
                    float d = (b.corePosition - from).magnitude;
                    if (d < distance) { distance = d; nearest = b; }
                }
            }
            if (!nearest || distance > ReachMeters) return false;
            Vector3 eye = self.inputBank ? self.inputBank.aimOrigin : from;
            return Perception.HasLineOfSight(eye, nearest.corePosition);
        }

        private void TrackOrder(Stats s, Order order, Vector3 pos)
        {
            if (order != s.order)
            {
                if (s.order != null && HasPlace(s.order))
                {
                    Vector3 point = s.order.Destination();
                    Vector3 d = point - pos;
                    d.y *= 0.4f;
                    if (d.magnitude <= DoneMeters)
                    {
                        s.done++;
                        float seconds = Time.time - s.order.startedAt;
                        Vector3 way = point - s.orderStart;
                        way.y = 0f;
                        if (seconds > 0.5f) s.speedSum += way.magnitude / seconds;
                    }
                    else s.dropped++;
                }
                // an order given before the measurement started says nothing about this window
                s.order = order != null && order.Issued >= startedAt ? order : null;
                s.orderStart = pos;
                s.orderReacted = false;
                if (s.order == null) return;
            }
            if (order == null || s.order == null || s.orderReacted || !HasPlace(order)) return;
            Vector3 moved = pos - s.orderStart;
            moved.y = 0f;
            if (moved.magnitude < ReactMeters) return;
            s.orderReacted = true;
            s.reacted++;
            s.reactSum += Time.time - order.Issued;
        }

        private static bool HasPlace(Order o) =>
            o.Kind == OrderKind.Move || o.Kind == OrderKind.AttackMove || o.Kind == OrderKind.Retreat;

        private static string TraceLine(Hero hero)
        {
            HeroBrain brain = hero.Brain;
            CharacterBody body = hero.Body;
            if (!brain || !body || !hero.IsAlive) return $"{hero} dead";
            BaseAI ai = hero.Master ? hero.Master.GetComponent<BaseAI>() : null;
            CharacterBody enemy = brain.Enemy;
            string enemyText = enemy ? $"{enemy.GetDisplayName()}({(enemy.corePosition - body.corePosition).magnitude:0}m)" : "-";
            string aiEnemy = ai && ai.currentEnemy.gameObject ? ai.currentEnemy.gameObject.name.Replace("(Clone)", "") : "-";
            Vector3 v = body.characterMotor ? body.characterMotor.velocity : Vector3.zero;
            v.y = 0f;
            HealthComponent hc = body.healthComponent;
            Tether t = brain.Tether;
            string tether = t.Active ? $" tether={t.Radius:0}@{t.Distance(body.footPosition):0}" : "";
            // line of sight three ways: the AI's cached flag, its test right now, our own ray (they should agree)
            string los = "";
            if (ai && ai.currentEnemy.gameObject)
            {
                bool now = ai.currentEnemy.TestLOSNow();
                bool ours = enemy && Perception.HasLineOfSight(body.inputBank.aimOrigin, enemy.corePosition);
                los = $" los={(ai.currentEnemy.hasLoS ? 1 : 0)}{(now ? 1 : 0)}{(ours ? 1 : 0)}" +
                      $"{(ai.currentEnemy.gameObject == (enemy ? enemy.gameObject : null) ? "" : " aiOther")}";
            }
            float skillAgo = ai ? ai.bodyLastSkillActivation.activationTime.timeSince : -1f;
            string dy = enemy ? $" dy={enemy.footPosition.y - body.footPosition.y:0}" : "";
            return $"{hero} {brain.Mode} '{brain.DebugState}' order={(brain.Current != null ? brain.Current.ToString() : "-")} " +
                   $"enemy={enemyText}{dy}{los} ai={aiEnemy} drv={(ai ? ai.selectedSkilldriverName : "-")} skill={Mathf.Min(skillAgo, 99f):0.0}s ago" +
                   $"{tether}{(brain.StandStill ? " still" : "")}{(brain.MobilitySkills ? "" : " nomob")} hp={hc.combinedHealthFraction:0.00} " +
                   $"v={v.magnitude:0.0} pos={body.footPosition:0}";
        }

        internal string Report()
        {
            float window = Time.time - startedAt;
            var sb = new StringBuilder($"hc_ai report: {window:0}s");
            HighCommandRun run = HighCommandRun.Instance;
            if (run == null) return sb.Append(" (no run)").ToString();
            float sumCombat = 0f, sumPassive = 0f, sumStanding = 0f, sumDealt = 0f;
            int sumDeaths = 0;
            foreach (Hero hero in run.Army.Heroes)
            {
                Stats s = For(hero);
                float a = Mathf.Max(0.01f, s.alive);
                sb.Append($"\n  {hero} alive {s.alive:0}s | idle {s.idle / a:P0} fight {s.fight / a:P0} go {s.go / a:P0} cast {s.cast / a:P0} flee {s.flee / a:P0}");
                sb.Append($" | combat {s.combat:0}s passive {s.passive:0.0}s ({(s.combat > 0f ? s.passive / s.combat : 0f):P0}) standing {s.standing:0.0}s stalled {s.stalled:0.0}s stuck {s.stuckEpisodes}");
                sb.Append($" | skills [{s.skills[0]},{s.skills[1]},{s.skills[2]},{s.skills[3]}] dmg {s.dealt:0} ({s.dealt / a:0}/s) taken {s.taken:0} deaths {s.deaths} kills {hero.Kills - s.killsAtStart}");
                sb.Append($" | orders done {s.done} dropped {s.dropped} react {(s.reacted > 0 ? s.reactSum / s.reacted : 0f):0.00}s speed {(s.done > 0 ? s.speedSum / s.done : 0f):0.0}m/s holdDrift {s.holdDrift:0.0}m");
                if (s.zone > 0f) sb.Append($" | in zone {s.inZone / s.zone:P0} of {s.zone:0}s");
                if (s.equipment > 0) sb.Append($" | equipment used {s.equipment}");
                sumCombat += s.combat;
                sumPassive += s.passive;
                sumStanding += s.standing;
                sumDealt += s.dealt;
                sumDeaths += s.deaths;
            }
            float inReach = Mathf.Max(0.01f, sumCombat);
            sb.Append($"\n  ARMY passive {sumPassive / inReach:P0} standing {sumStanding / inReach:P0} of {sumCombat:0}s in reach, " +
                      $"damage {sumDealt:0} ({sumDealt / Mathf.Max(1f, window):0}/s), deaths {sumDeaths}");
            return sb.ToString();
        }
    }
}
