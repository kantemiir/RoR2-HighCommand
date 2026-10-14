using System.Collections.Generic;
using HighCommand.Army;
using RoR2;
using RoR2.CharacterAI;
using UnityEngine;

namespace HighCommand.Orders
{
    public enum BrainMode : byte
    {
        Idle,   // stand where you are
        Fight,  // fight Enemy from inside Tether
        Go,     // travel to Goal; no fighting
        Cast    // aim at CastPoint / CastTarget and use CastSlot once
    }

    /// <summary>
    /// The decision layer of one hero (server only). It holds the order queue and the stance, looks around a few times a
    /// second and publishes an intent for the body - what to do (Mode), where (Goal, Tether), against whom (Enemy) and
    /// with which limits (StandStill, MobilitySkills). <see cref="HeroAIState"/> turns the intent into button presses
    /// every physics frame.
    ///
    /// Priorities, highest first:
    ///  1. an order that must not be interrupted: move, retreat, hold, use an object, cast;
    ///  2. survival: a hurt hero under attack runs (only during orders that allow it, see <see cref="SurvivalInstinct"/>);
    ///  3. the current order: attack, attack-move, patrol, follow, seek;
    ///  4. no order: the stance - fight what comes near the post, help friends who fight, pick up loot nearby, go back.
    /// </summary>
    public sealed class HeroBrain : MonoBehaviour
    {
        // ---- distances and times of the orders (the tunable ones are in AiTuning / the hero profile) ----------
        private const float ArriveDistance = 3.2f;
        private const float ArriveVerticalShare = 0.4f;  // the goal point is often a few meters above the node
        private const float SprintBeyond = 10f;          // a walk longer than this is run
        private const float IdleSprintBeyond = 22f;
        private const float GoalMovedReplan = 2f;        // the body re-plans at once when the goal moves farther
        private const float HoldSlack = 2f;              // pushed this far beyond the hold circle: walk back first
        private const float MeleeHoldReach = 4.5f;       // a melee hero on hold attacks enemies this far beyond its circle
        private const float FollowDistance = 7.5f;
        private const float FollowSprintBeyond = 20f;
        private const float FollowFightDistance = 22f;   // the leader is closer than this: free to fight
        private const float FollowAcquire = 28f;
        private const float FollowLeash = 26f;
        private const float ZoneFightShare = 0.85f;      // a hero in a charging zone fights from this share of its radius
        private const float BurstRadius = 20f;           // "burst" and "escape" skills count enemies this close
        private const float LowHealthShare = 1.6f;       // "escape" skills: health below retreat line x this
        private const float AttackingGrace = 1.2f;       // a skill used this recently: the hero is busy fighting
        private const float ProgressMeters = 1.5f;       // getting this much closer to the target is progress
        private const float UnreachableMemory = 8f;      // seconds a target given up is left alone
        private const float MeleeHitReach = 6f;          // the survivors' melee drivers strike within this driver distance
        private const float BigTargetPatience = 2f;      // a boss is chased this many times longer before it is given up
        private const float SelfDefenseRadius = 25f;     // a hero away from its post fights what is this close to it
        private const int FocusStrikesToGiveUp = 3;      // an attack order ends after the target was this often out of reach
        private const float LostTargetSeconds = 3f;      // a cloaked focus target is waited for this long
        private const float DefaultAttackRange = 30f;    // survivors whose drivers say "any distance"
        private const float MinAttackRange = 4f, MaxAttackRange = 60f;
        private const float RangeMargin = 2f;            // ranged heroes stand this much inside their reach
        private const float MinFightRange = 6f;
        private const float MeleeRangeLimit = 10f;       // a profile-less survivor whose primary reaches less is melee
        private const float FallbackStandoff = 18f;      // ... and one that reaches farther keeps this distance
        private const float CastTimeout = 3f;            // a cast that did not happen by then is dropped

        // ---- set from outside ----------------------------------------------------------------------------------
        public Hero Hero { get; internal set; }

        public Stance Stance = Stance.Aggressive;

        /// <summary>Auto-use switches: skill slots 0..3 and the equipment (4). Off = the AI never presses it.</summary>
        public readonly bool[] AutoSlot = { true, true, true, true, true };

        /// <summary>Where the hero stands when it has nothing to do.</summary>
        public Vector3 Anchor;

        /// <summary>
        /// While a holdout zone (teleporter, pillar ...) charges, RunFlow sets its centre and radius here: an idle hero
        /// then stands inside it, at its own spot, and fights from there. Radius 0 = no zone.
        /// </summary>
        public Vector3 ZoneCenter;
        public float ZoneRadius;

        public Order Current { get; private set; }

        public int QueuedCount => queue.Count;

        /// <summary>The orders waiting after the current one (read-only; the overlay draws the route from it).</summary>
        public IReadOnlyList<Order> Queue => queue;

        /// <summary>A hero could not use an object it was sent to (no gold, nothing left, unreachable). Host only.</summary>
        public static event System.Action<Hero, GameObject, InteractFailure> InteractFailed;

        // ---- intent (read by HeroAIState) ------------------------------------------------------------------------
        public BrainMode Mode { get; private set; } = BrainMode.Idle;
        public Vector3 Goal { get; private set; }
        public bool Sprint { get; private set; }
        public CharacterBody Enemy { get; private set; }

        /// <summary>The circle the hero fights from (free = none).</summary>
        public Tether Tether { get; private set; }

        /// <summary>Fight without walking (passive stance): shoot what is in reach, never chase.</summary>
        public bool StandStill { get; private set; }

        /// <summary>Skills that carry the hero somewhere (dashes, leaps, blinks) may be used.</summary>
        public bool MobilitySkills { get; private set; } = true;

        public int CastSlot { get; private set; } = -1;
        public Vector3 CastPoint { get; private set; }
        public CharacterBody CastTarget { get; private set; }

        /// <summary>Bumped whenever the body should react at once (another mode, target or goal).</summary>
        public int IntentVersion { get; private set; }

        public string DebugState { get; private set; } = "";

        /// <summary>Set by the body: it tries to walk but gets nowhere.</summary>
        public bool Stuck { get; internal set; }

        /// <summary>True while the hero runs from a fight it is losing.</summary>
        public bool Fleeing => survival.Fleeing;

        /// <summary>Running from enemies: fleeing on its own or ordered to retreat (its "get away" skill may be used).</summary>
        public bool Escaping => Fleeing || (Current != null && Current.Kind == OrderKind.Retreat);

        // ---- context for the skill rules (read by the body) ------------------------------------------------------
        /// <summary>Hostile bodies within <see cref="BurstRadius"/> m (refreshed every think).</summary>
        public int EnemiesNear { get; private set; }
        public bool LowHealth { get; private set; }
        public float TargetDistance { get; private set; } = float.MaxValue;
        public bool TargetIsBig { get; private set; }

        /// <summary>How far the hero's own attacks reach (from its skill drivers).</summary>
        public float AttackRange { get; private set; } = DefaultAttackRange;

        /// <summary>How far its farthest skill that does not move it reaches (at least <see cref="AttackRange"/>).</summary>
        public float SkillReach { get; private set; } = DefaultAttackRange;

        /// <summary>
        /// A melee hero fights at arm's length; the others keep <see cref="FightRange"/>. A survivor without its own profile
        /// (a modded one) is judged by the reach of its primary attack.
        /// </summary>
        public bool Melee => IsFallbackProfile ? AttackRange < MeleeRangeLimit : Profile.standoff <= 0f;

        /// <summary>The distance a ranged hero keeps from its target (0 for melee).</summary>
        public float FightRange
        {
            get
            {
                if (Melee) return 0f;
                float wanted = IsFallbackProfile ? FallbackStandoff : Profile.standoff;
                return Mathf.Max(MinFightRange, Mathf.Min(wanted, AttackRange - RangeMargin));
            }
        }

        private bool IsFallbackProfile => Profile.survivor == "*";

        private HeroProfile profile;

        /// <summary>This hero's data-driven behaviour (ranges, flee threshold, per-slot skill rules).</summary>
        public HeroProfile Profile
        {
            get
            {
                if (profile == null)
                    profile = ProfileStore.For(Hero != null && Hero.Survivor ? Hero.Survivor.cachedName : null);
                return profile;
            }
        }

        // ---- parts --------------------------------------------------------------------------------------------
        private readonly List<Order> queue = new List<Order>(4);
        private readonly TargetPicker picker = new TargetPicker();
        private readonly SurvivalInstinct survival = new SurvivalInstinct();
        private readonly EnemySeeker seeker = new EnemySeeker();
        private LootErrands loot;

        private BaseAI ai;
        private CharacterBody lastBody;
        private bool initialised;
        private bool rangeKnown;
        private float thinkTimer;
        private float castStarted;

        private Vector3 zoneSpot, zoneSpotKey;
        private float zoneSpotRadius;

        // the target being chased: did the hero get closer or hit it lately?
        private CharacterBody watchTarget;
        private float watchBest, watchSince;

        private void Awake()
        {
            loot = new LootErrands(this);
            thinkTimer = Random.Range(0f, AiTuning.ThinkInterval.Value); // heroes recruited together do not think in step
        }

        // ---- commands -------------------------------------------------------------------------------------------

        public void Issue(Order order, bool queued)
        {
            if (order == null) return;
            order.Issued = Time.time;
            if (!queued)
            {
                queue.Clear();
                Current = null; // dropped without ceremony; the new order starts at once
            }
            queue.Add(order);
            if (Current == null) thinkTimer = 0f;
        }

        /// <summary>
        /// A trip to an object (a chest, a shrine ...) that goes in front of what the hero was doing: afterwards the hero
        /// takes up its own order again (walks back to its hold spot, goes on patrolling ...) or, with no order, returns
        /// to its post. With <paramref name="queued"/> the trip is added after the trips already planned (a route through
        /// several chests); without it, planned trips are dropped.
        /// </summary>
        public void Errand(Order order, bool queued)
        {
            if (order == null) return;
            order.Issued = Time.time;
            order.Errand = true;

            if (!queued)
            {
                for (int i = queue.Count - 1; i >= 0; i--) if (queue[i].Errand) queue.RemoveAt(i);
                if (Current != null && Current.Errand) Current = null;
            }

            // the hero's own order waits behind the trips
            if (Current != null && !Current.Errand)
            {
                Order own = Current;
                Current = null;
                switch (own.Kind)
                {
                    case OrderKind.Hold:
                        // back to the spot first: a hold order starts where the hero stands
                        queue.Insert(0, own);
                        queue.Insert(0, new Order { Kind = OrderKind.Move, Point = own.Point, Issued = Time.time });
                        break;
                    case OrderKind.Cast:
                    case OrderKind.Stop:
                        break; // a moment's order: not worth taking up again
                    default:
                        queue.Insert(0, own);
                        break;
                }
            }

            int at = 0;
            if (Current == null || Current.Errand)
                for (int i = 0; i < queue.Count; i++) if (queue[i].Errand) at = i + 1;
            queue.Insert(at, order);
            if (Current == null) thinkTimer = 0f;
        }

        /// <summary>True while trips to objects are going on or planned.</summary>
        public bool HasErrands
        {
            get
            {
                if (Current != null && Current.Errand) return true;
                for (int i = 0; i < queue.Count; i++) if (queue[i].Errand) return true;
                return false;
            }
        }

        /// <summary>Where the last planned trip ends (the hero's own position when none is planned).</summary>
        public Vector3 ErrandsEnd(Vector3 here)
        {
            Vector3 end = here;
            if (Current != null && Current.Errand) end = Current.Destination();
            for (int i = 0; i < queue.Count; i++) if (queue[i].Errand) end = queue[i].Destination();
            return end;
        }

        public void ClearOrders(bool setAnchorHere)
        {
            queue.Clear();
            Current = null;
            Enemy = null;
            if (setAnchorHere && Hero != null && Hero.Body) Anchor = Hero.Body.footPosition;
            thinkTimer = 0f;
        }

        public void SetStance(Stance s) => Stance = s;

        /// <summary>Chests the purse can pay for now that no other hero is going to (the "seek chests" button).</summary>
        internal int CountChestsToSeek() => loot.CountChests(Hero != null && Hero.Body ? Hero.Body.footPosition : Vector3.zero);

        internal void NotifyCastPressed()
        {
            if (Current != null && Current.Kind == OrderKind.Cast) Finish();
        }

        // ---- loop ---------------------------------------------------------------------------------------------

        private void FixedUpdate()
        {
            thinkTimer -= Time.fixedDeltaTime;
            if (thinkTimer > 0f) return;
            thinkTimer = AiTuning.ThinkInterval.Value;
            long started = Dev.Perf.Begin();
            try { Think(); }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"HeroBrain.Think failed for {Hero}: {e}");
                Mode = BrainMode.Idle;
                Enemy = null;
            }
            Dev.Perf.End(ref Dev.Perf.Brain, started);
        }

        private void Think()
        {
            CharacterBody self = Hero != null ? Hero.Body : null;
            if (!self || !self.healthComponent || !self.healthComponent.alive)
            {
                Mode = BrainMode.Idle;
                Enemy = null;
                DebugState = "dead";
                return;
            }

            // A different body (a new stage, a revival): old orders and the old anchor belong to the old place.
            if (self != lastBody) ResetForBody(self);

            Vector3 pos = self.footPosition;
            if (!initialised)
            {
                // Wait until the hero stands on its own feet (a passenger of the drop pod is still in the air).
                if (self.currentVehicle) { Mode = BrainMode.Idle; DebugState = "in pod"; return; }
                initialised = true;
                Anchor = pos;
                if (Settings.VerboseLog.Value) Plugin.Log.LogInfo($"{Hero}: anchor set at {pos}");
            }

            if (!ai && Hero.Master) ai = Hero.Master.GetComponent<BaseAI>();
            if (!rangeKnown && ai) LearnAttackRange();

            if (Current == null && queue.Count > 0) StartNextOrder(pos);
            UpdateContext(self, pos);

            BrainMode oldMode = Mode;
            CharacterBody oldEnemy = Enemy;
            Vector3 oldGoal = Goal;
            int oldSlot = CastSlot;
            ResetIntent();

            Order o = Current;
            if (survival.Update(Hero, self, Profile, FleeAllowed(o))) ThinkFlee(pos);
            else if (o == null) ThinkIdle(self, pos);
            else ThinkOrder(o, self, pos);

            WatchEngagement(self);

            TargetIsBig = EnemyTraits.IsBig(Enemy);
            TargetDistance = Enemy ? (Enemy.corePosition - self.corePosition).magnitude : float.MaxValue;
            if (Mode != oldMode || Enemy != oldEnemy || CastSlot != oldSlot
                || (Mode == BrainMode.Go && (Goal - oldGoal).sqrMagnitude > GoalMovedReplan * GoalMovedReplan))
                IntentVersion++;
            PushEnemyToAi();
        }

        private void ResetForBody(CharacterBody self)
        {
            lastBody = self;
            initialised = false;
            queue.Clear();
            Current = null;
            Enemy = null;
            watchTarget = null;
            picker.Forget();
            survival.Reset();
            seeker.Reset();
            loot.Reset();
        }

        private void StartNextOrder(Vector3 pos)
        {
            Current = queue[0];
            queue.RemoveAt(0);
            Current.startedAt = Time.time;
            castStarted = Time.time;
            if (Current.Kind == OrderKind.Stop)
            {
                Anchor = pos;
                Finish();
            }
        }

        private void ResetIntent()
        {
            Sprint = false;
            StandStill = false;
            MobilitySkills = true;
            Tether = Tether.Free;
            CastSlot = -1;
        }

        // From the survivor's own skill drivers: how far its primary attack reaches (where a ranged hero may stand), and
        // how far any skill that does not move the hero reaches (what a hero on hold can still hit: a melee hero's spit,
        // pylon or grenade).
        private void LearnAttackRange()
        {
            rangeKnown = true;
            float primary = 0f, any = 0f;
            AISkillDriver[] drivers = ai.skillDrivers;
            CharacterBody self = Hero.Body;
            for (int i = 0; drivers != null && i < drivers.Length; i++)
            {
                AISkillDriver d = drivers[i];
                if (!d || d.skillSlot == SkillSlot.None || d.buttonPressType == AISkillDriver.ButtonPressType.Abstain) continue;
                if (float.IsInfinity(d.maxDistance)) continue;
                int slot = (int)d.skillSlot;
                if (d.skillSlot == SkillSlot.Primary) primary = Mathf.Max(primary, d.maxDistance);
                if (!SkillGate.IsMobility(Profile, slot, self) && Profile.RuleFor(slot) != HeroProfile.RuleEscape) any = Mathf.Max(any, d.maxDistance);
            }
            AttackRange = primary > 0f ? Mathf.Clamp(primary, MinAttackRange, MaxAttackRange) : DefaultAttackRange;
            SkillReach = Mathf.Clamp(Mathf.Max(any, AttackRange), MinAttackRange, MaxAttackRange);
            if (Settings.VerboseLog.Value)
                Plugin.Log.LogInfo($"{Hero}: attack range {AttackRange:0} m, skill reach {SkillReach:0} m, fight range {FightRange:0} m");
        }

        private void UpdateContext(CharacterBody self, Vector3 pos)
        {
            HealthComponent hc = self.healthComponent;
            LowHealth = hc.combinedHealthFraction < Profile.retreatHealth * LowHealthShare;
            EnemiesNear = Perception.CountEnemies(pos, BurstRadius, self.teamComponent.teamIndex);
        }

        // ---- intents ----------------------------------------------------------------------------------------------

        private void Fight(CharacterBody enemy, Tether tether, bool standStill, bool mobility, string state)
        {
            Enemy = enemy;
            Mode = BrainMode.Fight;
            Tether = tether;
            StandStill = standStill;
            MobilitySkills = mobility && !standStill;
            DebugState = state;
        }

        /// <param name="sprintBeyond">the hero runs when the goal is farther than this (0 = always, infinity = never)</param>
        private void GoTo(Vector3 destination, Vector3 pos, float sprintBeyond, string state)
        {
            Enemy = null;
            Mode = BrainMode.Go;
            Goal = destination;
            Sprint = (destination - pos).sqrMagnitude > sprintBeyond * sprintBeyond;
            DebugState = state;
        }

        private void Stand(string state)
        {
            Enemy = null;
            Mode = BrainMode.Idle;
            DebugState = state;
        }

        private TargetPicker.Query Search(Vector3 center, float radius, Tether tether, float assist = 0f, bool onlyAttackers = false) =>
            new TargetPicker.Query
            {
                Center = center,
                Radius = radius,
                AssistRadius = assist,
                Tether = tether,
                RequireSight = true,
                OnlyAttackers = onlyAttackers
            };

        private CharacterBody Pick(CharacterBody self, in TargetPicker.Query q) => picker.Pick(self, Melee, SkillReach, q, Enemy);

        // ---- survival -------------------------------------------------------------------------------------------

        // An explicit move, retreat, hold, use or cast is never cut short by the instinct to run.
        private static bool FleeAllowed(Order o)
        {
            if (o == null) return true;
            switch (o.Kind)
            {
                case OrderKind.AttackMove:
                case OrderKind.Attack:
                case OrderKind.Patrol:
                case OrderKind.Follow:
                case OrderKind.SeekEnemies:
                case OrderKind.SeekChests:
                    return true;
                case OrderKind.Interact:
                    return o.Errand; // a trip to a chest is not worth dying for
                default:
                    return false;
            }
        }

        private void ThinkFlee(Vector3 pos)
        {
            GoTo(survival.Goal(Hero, pos), pos, sprintBeyond: 0f, "fleeing");
        }

        // ---- no order: the stance ---------------------------------------------------------------------------

        private void ThinkIdle(CharacterBody self, Vector3 pos)
        {
            bool zone = ZoneRadius > 0f;
            if (zone) Anchor = ZoneSpot();

            float acquire = Stance == Stance.Aggressive ? Profile.acquireAggressive : Profile.acquireDefensive;
            Tether tether = zone
                ? new Tether(ZoneCenter, ZoneRadius * ZoneFightShare)
                : new Tether(Anchor, Stance == Stance.Aggressive ? Profile.leashAggressive : Profile.leashDefensive);

            if (Stance == Stance.Passive)
            {
                // only answer whoever is hitting this hero, from where it stands
                TargetPicker.Query q = Search(pos, acquire, Tether.Free, onlyAttackers: true);
                q.StrictSight = true;
                CharacterBody attacker = Pick(self, q);
                if (attacker) { Fight(attacker, new Tether(pos, 0.01f), standStill: true, mobility: false, "idle: answering fire"); return; }
            }
            else
            {
                CharacterBody enemy = Pick(self, Search(pos, acquire, tether, AiTuning.AssistRange.Value));
                if (enemy) { Fight(enemy, tether, standStill: false, mobility: true, "idle: fighting"); return; }

                // far from its post (knocked off a ledge, back from a long chase): monsters right here are fought, not
                // walked past on the way home
                if (!zone && !tether.Contains(pos))
                {
                    var local = new Tether(pos, SelfDefenseRadius);
                    enemy = Pick(self, Search(pos, SelfDefenseRadius, local));
                    if (enemy) { Fight(enemy, local, standStill: false, mobility: true, "idle: defending itself"); return; }
                }
            }

            bool calm = !zone && EnemiesNear == 0 && self.outOfDanger;
            if (loot.TryNearbyTrip(pos, calm))
            {
                Stand("idle: going for loot");
                thinkTimer = 0f; // the trip starts at the next think
                return;
            }
            ReturnToAnchor(pos);
        }

        private void ReturnToAnchor(Vector3 pos)
        {
            float away = (pos - Anchor).magnitude;
            if (away > ArriveDistance + 1f) GoTo(Anchor, pos, IdleSprintBeyond, "idle: returning to post");
            else Stand("idle");
        }

        // A stable place for this hero inside the zone (heroes must not all stack on the centre).
        private Vector3 ZoneSpot()
        {
            const float GoldenAngle = 2.399963f; // neighbours spread evenly
            const float InnerShare = 0.22f, RingShare = 0.34f;
            if ((ZoneCenter - zoneSpotKey).sqrMagnitude > 1f || Mathf.Abs(ZoneRadius - zoneSpotRadius) > 1.5f)
            {
                zoneSpotKey = ZoneCenter;
                zoneSpotRadius = ZoneRadius;
                int id = Hero != null ? Hero.Id : 0;
                float angle = id * GoldenAngle;
                float radius = ZoneRadius * (InnerShare + RingShare * (((id * 7) % 5) / 4f));
                Vector3 p = ZoneCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                zoneSpot = Formation.SnapToGround(p);
            }
            return zoneSpot;
        }

        // ---- orders ---------------------------------------------------------------------------------------------

        private void ThinkOrder(Order o, CharacterBody self, Vector3 pos)
        {
            switch (o.Kind)
            {
                case OrderKind.Move:
                    GoTo(o.Point, pos, SprintBeyond, "move");
                    if (Arrived(pos, o.Point)) { Anchor = o.Point; Finish(); }
                    break;

                case OrderKind.Retreat:
                    GoTo(o.Point, pos, sprintBeyond: 0f, "retreat");
                    if (Arrived(pos, o.Point)) { Anchor = o.Point; Finish(); }
                    break;

                case OrderKind.AttackMove:
                    ThinkAttackMove(o, o.Point, self, pos, patrol: false);
                    break;

                case OrderKind.Patrol:
                    ThinkAttackMove(o, o.patrolToSecond ? o.Point2 : o.Point, self, pos, patrol: true);
                    break;

                case OrderKind.Attack:
                    ThinkAttack(o, self, pos);
                    break;

                case OrderKind.Hold:
                    ThinkHold(o, self, pos);
                    break;

                case OrderKind.Follow:
                    ThinkFollow(o, self, pos);
                    break;

                case OrderKind.Interact:
                    ThinkInteract(o, self, pos);
                    break;

                case OrderKind.Cast:
                    Enemy = null;
                    Mode = BrainMode.Cast;
                    CastSlot = o.Slot;
                    CastPoint = o.Point;
                    CastTarget = o.Target ? o.Target.GetComponent<CharacterBody>() : null;
                    DebugState = "cast";
                    if (Time.time - castStarted > CastTimeout) Finish(); // could not be cast: do not block the queue
                    break;

                case OrderKind.SeekEnemies:
                    ThinkSeekEnemies(self, pos);
                    break;

                case OrderKind.SeekChests:
                    ThinkSeekChests(self, pos);
                    break;

                default:
                    Finish();
                    break;
            }
        }

        // Marching to a point and fighting what is met on the way: a fight is kept within the leash around the place
        // where it started, then the march goes on.
        private void ThinkAttackMove(Order o, Vector3 destination, CharacterBody self, Vector3 pos, bool patrol)
        {
            Tether leash = new Tether(o.engaged ? o.engagedAt : pos, AiTuning.AttackMoveLeash.Value);
            CharacterBody enemy = Pick(self, Search(pos, AiTuning.AttackMoveRange.Value, leash));
            if (enemy)
            {
                if (!o.engaged) { o.engaged = true; o.engagedAt = pos; }
                Fight(enemy, leash, standStill: false, mobility: true, patrol ? "patrol: fighting" : "attack-move: fighting");
                return;
            }

            o.engaged = false;
            GoTo(destination, pos, SprintBeyond, patrol ? "patrol" : "attack-move");
            if (!Arrived(pos, destination)) return;
            if (patrol) o.patrolToSecond = !o.patrolToSecond;
            else { Anchor = destination; Finish(); }
        }

        // Focus fire: this enemy and no other. The order ends with its death; a target that hides (cloak) is waited for
        // a moment, one that keeps being out of reach is given up so the hero does not run in circles for ever.
        private void ThinkAttack(Order o, CharacterBody self, Vector3 pos)
        {
            CharacterBody target = o.Target ? o.Target.GetComponent<CharacterBody>() : null;
            bool dead = !target || !target.healthComponent || !target.healthComponent.alive;
            if (dead || o.unreachableStrikes >= FocusStrikesToGiveUp)
            {
                if (!dead && Settings.VerboseLog.Value) Plugin.Log.LogInfo($"{Hero}: gave up the attack on {target.name} (out of reach)");
                Anchor = pos; // a hero that killed its target stays there instead of running back
                Finish();
                Stand(dead ? "attack: done" : "attack: out of reach");
                return;
            }

            if (!Perception.IsTargetable(target, self.teamComponent.teamIndex))
            {
                if (o.lostAt < 0f) o.lostAt = Time.time;
                if (Time.time - o.lostAt > LostTargetSeconds) { Anchor = pos; Finish(); Stand("attack: target lost"); }
                else Stand("attack: waiting for the target");
                return;
            }
            o.lostAt = -1f;
            Fight(target, Tether.Free, standStill: false, mobility: true, "attack");
        }

        // Hold position: fight from the spot. A melee hero may step a little (HoldRadius) to reach an enemy; skills that
        // carry the hero away are not used; a hero pushed off the spot walks back.
        private void ThinkHold(Order o, CharacterBody self, Vector3 pos)
        {
            float radius = AiTuning.HoldRadius.Value;
            var circle = new Tether(o.Point, Mathf.Max(radius, 0.5f));
            if (circle.Distance(pos) > radius + HoldSlack)
            {
                GoTo(o.Point, pos, SprintBeyond, "hold: back to the spot");
                return;
            }

            // what the hero can hit from its circle: its weapon, or a ranged skill of a melee hero (spit, pylon, grenade)
            float reach = radius + Mathf.Max(SkillReach, Melee ? MeleeHoldReach : AttackRange);
            TargetPicker.Query q = Search(o.Point, reach, circle);
            q.StrictSight = true; // it cannot walk round to a target out of sight
            CharacterBody enemy = Pick(self, q);
            if (enemy) { Fight(enemy, circle, standStill: false, mobility: false, "hold: fighting"); return; }

            if (circle.Distance(pos) > ArriveDistance) GoTo(o.Point, pos, float.PositiveInfinity, "hold: back to the spot");
            else Stand("hold");
        }

        private void ThinkFollow(Order o, CharacterBody self, Vector3 pos)
        {
            CharacterBody leader = o.Target ? o.Target.GetComponent<CharacterBody>() : null;
            if (!leader || !leader.healthComponent || !leader.healthComponent.alive)
            {
                Finish();
                Stand("follow: leader gone");
                return;
            }
            Vector3 lp = leader.footPosition;
            float dist = (lp - pos).magnitude;

            var leash = new Tether(lp, FollowLeash);
            CharacterBody enemy = dist < FollowFightDistance ? Pick(self, Search(lp, FollowAcquire, leash)) : null;
            if (enemy) { Fight(enemy, leash, standStill: false, mobility: true, "follow: fighting"); return; }

            if (dist > FollowDistance) GoTo(lp, pos, FollowSprintBeyond, "follow");
            else Stand("follow: close");
        }

        private void ThinkInteract(Order o, CharacterBody self, Vector3 pos)
        {
            const float ReachHorizontal = 3f, ReachVertical = 4.5f, StuckGiveUp = 8f;
            GameObject target = o.Target;
            bool teleporter = target && target.GetComponent<TeleporterInteraction>();
            if (!target || (!teleporter && Interactables.IsUsed(target)))
            {
                // gone, or somebody else got it first: nothing to say, the hero goes back to what it was doing
                Finish();
                Stand("interact: nothing left");
                return;
            }

            // the spot beside the object (a big chest or a shop is wider than the reach around its centre)
            if (!o.reachKnown)
            {
                o.reach = Interactables.ReachPoint(target, pos);
                o.reachKnown = true;
            }
            Vector3 to = o.reach - pos;
            float dy = to.y;
            to.y = 0f;
            bool there = to.magnitude <= ReachHorizontal && Mathf.Abs(dy) < ReachVertical;

            if (!there)
            {
                bool gaveUp = Time.time - o.startedAt > o.walkBudget || (Stuck && Time.time - o.startedAt > StuckGiveUp);
                if (!gaveUp)
                {
                    GoTo(o.reach, pos, SprintBeyond, "interact: walking");
                    return;
                }
                if (!teleporter)
                {
                    if (!o.auto) InteractFailed?.Invoke(Hero, target, InteractFailure.Unreachable);
                    else loot.Skip(target); // not tried again from here
                    Finish();
                    Stand("interact: unreachable");
                    return;
                }
                // the teleporter is used from wherever the hero got to: a hero stuck on the way must not hold up the stage
            }

            var interactor = self.GetComponent<Interactor>();
            var interactable = target.GetComponent<IInteractable>();
            string state = "interact: used";
            if (interactor && interactable != null)
            {
                Interactability can = interactable.GetInteractability(interactor);
                if (can == Interactability.Available) interactor.AttemptInteraction(target);
                else if (can == Interactability.ConditionsNotMet)
                {
                    bool gold = Interactables.TryGetPrice(target, out CostTypeIndex type, out int _) && type == CostTypeIndex.Money;
                    // a trip of the hero's own (a chest it went for) ends quietly: the purse was spent meanwhile
                    if (!o.auto) InteractFailed?.Invoke(Hero, target, gold ? InteractFailure.NoGold : InteractFailure.CannotPay);
                    state = "interact: cannot pay";
                }
            }
            Finish();
            Stand(state);
        }

        // Goes after the nearest enemy of the whole map, fights whatever it meets on the way, then takes the next one.
        // With nothing left the hero waits where it is (the order stays: new monsters are hunted as they come).
        private void ThinkSeekEnemies(CharacterBody self, Vector3 pos)
        {
            Anchor = pos;
            CharacterBody enemy = Pick(self, Search(pos, AiTuning.AttackMoveRange.Value, Tether.Free));
            if (enemy) { Fight(enemy, Tether.Free, standStill: false, mobility: true, "seek enemies: fighting"); return; }

            CharacterBody prey = seeker.Update(self, pos, out bool gaveUp);
            if (gaveUp) { Stand("seek enemies: out of reach, next"); return; }
            if (!prey) { Stand("seek enemies: none on the map"); return; }
            GoTo(prey.footPosition, pos, SprintBeyond, "seek enemies: going");
        }

        // Opens the chests of the whole map the purse can pay for, the nearest first (every hero its own one), and
        // fights back on the way. The order ends when no chest is left that the purse (minus what others will spend) can pay.
        private void ThinkSeekChests(CharacterBody self, Vector3 pos)
        {
            Anchor = pos;
            CharacterBody enemy = Pick(self, Search(pos, Profile.acquireDefensive, Tether.Free));
            if (enemy) { Fight(enemy, Tether.Free, standStill: false, mobility: true, "seek chests: fighting"); return; }

            Stand("seek chests: looking");
            if (!loot.PlanSeekTrip(pos, out bool waiting, out float distance))
            {
                Finish();
                Stand("seek chests: nothing left to buy");
            }
            else if (!waiting) DebugState = $"seek chests: going to one ({distance:0} m)";
        }

        // ---- helpers --------------------------------------------------------------------------------------------

        private static bool Arrived(Vector3 pos, Vector3 point)
        {
            Vector3 d = point - pos;
            d.y *= ArriveVerticalShare;
            return d.sqrMagnitude <= ArriveDistance * ArriveDistance;
        }

        private void Finish()
        {
            Current = null;
            thinkTimer = 0f;
        }

        // A target the hero chases without getting closer and without hitting it (a flier over a pit, a monster on a
        // ledge, a wall in the way) is given up for a while: the next think picks another one.
        private void WatchEngagement(CharacterBody self)
        {
            // a hero told to stay (hold, passive stance) cannot close in: that is no sign of an unreachable target
            bool held = StandStill || (Current != null && Current.Kind == OrderKind.Hold);
            if (Mode != BrainMode.Fight || !Enemy || held)
            {
                watchTarget = null;
                return;
            }
            float distance = (Enemy.corePosition - self.corePosition).magnitude;
            float now = Time.time;
            if (Enemy != watchTarget)
            {
                watchTarget = Enemy;
                watchBest = distance;
                watchSince = now;
                return;
            }
            bool attacking = ai && ai.bodyLastSkillActivation.activationTime.timeSince < AttackingGrace;
            if (attacking || distance < watchBest - ProgressMeters || InReachAndSeen(self))
            {
                watchBest = Mathf.Min(watchBest, distance);
                watchSince = now;
                return;
            }
            // a boss is worth a longer walk round than a beetle
            float patience = AiTuning.UnreachableSeconds.Value * (EnemyTraits.IsBig(Enemy) ? BigTargetPatience : 1f);
            if (now - watchSince < patience) return;

            picker.MarkUnreachable(Enemy, UnreachableMemory);
            if (Current != null && Current.Kind == OrderKind.Attack) Current.unreachableStrikes++;
            if (Settings.VerboseLog.Value) Plugin.Log.LogInfo($"{Hero}: cannot reach {Enemy.name} ({distance:0} m), taking another target");
            watchTarget = null;
            Stand("target out of reach");
            thinkTimer = 0f;
        }

        // Seen (our own ray: the AI's cached flag lags behind) and within the hero's reach: a skill on cooldown is no
        // reason to give the target up.
        private bool InReachAndSeen(CharacterBody self)
        {
            if (Perception.DriverDistance(self, Enemy) > (Melee ? MeleeHitReach : AttackRange)) return false;
            Vector3 eye = self.inputBank ? self.inputBank.aimOrigin : self.corePosition;
            return Perception.HasLineOfSight(eye, Enemy.corePosition);
        }

        private void PushEnemyToAi()
        {
            if (!ai) return;
            if (Mode == BrainMode.Fight && Enemy && Enemy.healthComponent && Enemy.healthComponent.alive)
            {
                if (ai.currentEnemy.gameObject != Enemy.gameObject)
                {
                    ai.currentEnemy.Reset();
                    ai.currentEnemy.gameObject = Enemy.gameObject;
                    ai.currentEnemy.bestHurtBox = ai.GetBestHurtBox(Enemy.gameObject);
                }
                ai.enemyAttention = ai.enemyAttentionDuration;
            }
            else if (ai.currentEnemy.gameObject)
            {
                // the brain alone decides whom to fight: the game's own search (any monster in sight) must not pull
                // the skill drivers towards something else
                ai.currentEnemy.Reset();
            }
        }
    }
}
