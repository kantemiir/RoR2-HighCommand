using EntityStates.AI;
using RoR2;
using RoR2.CharacterAI;
using RoR2.Navigation;
using UnityEngine;

namespace HighCommand.Orders
{
    /// <summary>
    /// The body-level executor of a hero (the game's own AI state machine runs it instead of the monster "Wander/Combat"
    /// states). Every physics frame it writes the same inputs a player would: move vector, aim, skill buttons, sprint,
    /// jump, equipment.
    ///
    /// The intent comes from <see cref="HeroBrain"/>. Which skill to press comes from the AISkillDriver rules the game
    /// ships for every survivor, evaluated by BaseAI - minus the skills <see cref="SkillGate"/> forbids at the moment:
    /// when the game's first choice is forbidden, the best allowed driver takes its place (otherwise a hero whose first
    /// rule is a held-back ultimate would never shoot at all). Walking in a fight is <see cref="CombatMover"/>'s, walking
    /// to a place uses the game's path with <see cref="StuckRecovery"/>.
    /// </summary>
    public class HeroAIState : BaseAIState
    {
        private const float UpdateInterval = 0.1f;
        private const float GoalMovedReplan = 2f;     // a goal that moved this far gets a new path at once
        private const float ArrivedSlack = 1.5f;      // this close to its goal a hero is not "stuck", it is there
        private const float SidestepLength = 4f;
        private const float StopDistance = 0.75f;     // a fight's walking point this close: stand
        private const float MeleeFallbackReach = 5f;  // a melee hero's primary hits within this driver distance
        private const int FallbackPulseFrames = 8;    // the fallback primary is held 6 of every 8 frames: charged shots
        private const int FallbackHeldFrames = 6;     // (Engineer's grenades, Captain's shotgun) are released and fire
        private const float DirectGoal = 12f;         // a goal this close, seen and on this level is walked to straight
        private const float DirectGoalClimb = 2.5f;
        private const float SprintToFarTarget = 10f;  // no skill driver: run to a target farther than this
        private const int EscapeFrames = 3;
        private const float CastAimSeconds = 0.2f;
        private const int CastFrames = 4;
        private const int StubGraphNodes = 20;

        private HeroBrain brain;
        private readonly CombatMover mover = new CombatMover();
        private readonly SkillGate gate = new SkillGate();
        private readonly StuckRecovery stuck = new StuckRecovery();
        private readonly EquipmentUser equipment = new EquipmentUser();
        private BaseAI.SkillDriverEvaluationContext evalContext;
        private BaseAI.OnBeginSkillDriverDelegate onBeginDriver;

        private float updateTimer;
        private Vector3 footPosition;
        private int seenIntent = -1;
        private Vector3 lastGoal;
        private CharacterBody lastEnemy;

        // the skill driver of the moment and the state of its button
        private AISkillDriver dominantDriver;
        private bool driverMeetsConditions;
        private SkillSlot driverSlot = SkillSlot.None;
        private bool aimConfirmationNeeded;
        private bool hasSkillButtonBeenUnheld;
        private bool hasSkillButtonBeenHeld;
        private bool freshDriver;
        private bool sprintInFight;
        private bool pathJumps;      // the hero follows the game's path now: the jumps it plans may be made
        private bool fallbackFire;   // no driver fits, but the enemy is seen and in reach: the primary is pressed anyway

        private float castPressTime = -1f;
        private int castFrames;
        private int escapeFrames;

        public override void OnEnter()
        {
            base.OnEnter();
            brain = characterMaster ? characterMaster.GetComponent<HeroBrain>() : null;
            if (ai)
            {
                ai.broadNavigationAgent.InvalidatePath();
                onBeginDriver = OnAiBeginSkillDriver;
                ai.onBeginSkillDriver += onBeginDriver;
            }
            if (body) stuck.Reset(body.footPosition);
        }

        public override void OnExit()
        {
            if (ai != null && onBeginDriver != null) ai.onBeginSkillDriver -= onBeginDriver;
            if (brain) brain.Stuck = false;
            base.OnExit();
        }

        // ---- the game's driver choice, with the forbidden skills taken out -------------------------------------------

        private void OnAiBeginSkillDriver(in BaseAI.SkillDriverEvaluation evaluation)
        {
            freshDriver = true;
            AISkillDriver chosen = evaluation.dominantSkillDriver;
            if (!chosen || !brain || DriverAllowed(chosen)) return;
            try { ChooseAllowedDriver(chosen); }
            catch (System.Exception e) { Plugin.Log.LogError($"{characterMaster.name}: driver re-selection failed: {e}"); }
        }

        // The same evaluation BaseAI does (first driver whose conditions hold), skipping what the gate forbids.
        private void ChooseAllowedDriver(AISkillDriver forbidden)
        {
            if (evalContext == null) evalContext = new BaseAI.SkillDriverEvaluationContext();
            evalContext.InitializeFromBaseAI(ai);
            try
            {
                AISkillDriver[] drivers = ai.skillDrivers;
                for (int i = 0; i < drivers.Length; i++)
                {
                    AISkillDriver d = drivers[i];
                    if (!d || !d.enabled || d == forbidden || !DriverAllowed(d)) continue;
                    BaseAI.SkillDriverEvaluation? e = ai.EvaluateSingleSkillDriver(d, evalContext);
                    if (!e.HasValue) continue;
                    ai.skillDriverEvaluation = e.Value;
                    ai.selectedSkilldriverName = d.customName;
                    d.OnSelected();
                    return;
                }
                ai.skillDriverEvaluation = default;
                ai.selectedSkilldriverName = "";
            }
            finally { evalContext.Clear(); }
        }

        // A driver that only walks (no skill) is CombatMover's business here; taking it would just block the skill
        // drivers listed after it (Commando's "back off within 15 m" rule kept it from ever shooting closer than that).
        // A deliberate pause (movement Stop) stays.
        private bool DriverAllowed(AISkillDriver d) =>
            d.skillSlot == SkillSlot.None
                ? d.movementType == AISkillDriver.MovementType.Stop
                : gate.Allows(brain, (int)d.skillSlot, d, body);

        // ---- update ---------------------------------------------------------------------------------------------------

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!ai || !body) return;

            float dt = GetDeltaTime();
            updateTimer -= dt;

            footPosition = body.temporaryPathfindingFootpositionDoNotUseWillBePatchedOut;
            BroadNavigationSystem.Agent agent = ai.broadNavigationAgent;
            agent.currentPosition = footPosition;

            // a new order, target or goal is acted on in this very frame, not at the next scheduled update
            bool react = brain && brain.IntentVersion != seenIntent;
            if (updateTimer > 0f && !react) return;
            updateTimer = UpdateInterval;
            if (react) OnNewIntent(agent);

            DropStubNodeGraph(agent);
            long started = Dev.Perf.Begin();
            UpdateAI(UpdateInterval);
            Dev.Perf.End(ref Dev.Perf.Executor, started);
        }

        private void OnNewIntent(BroadNavigationSystem.Agent agent)
        {
            seenIntent = brain.IntentVersion;
            bool replan = false;
            if (brain.Mode == BrainMode.Go && (brain.Goal - lastGoal).sqrMagnitude > GoalMovedReplan * GoalMovedReplan)
            {
                lastGoal = brain.Goal;
                replan = true;
            }
            if (brain.Mode == BrainMode.Fight && brain.Enemy != lastEnemy) replan = true;
            lastEnemy = brain.Enemy;
            if (replan) agent.InvalidatePath();
        }

        /// <summary>
        /// Scenes where the stock game never lets AI walk (the Bazaar) ship a stub node graph of a few nodes: every path
        /// request on it throws inside NodeGraph.ComputePath and sends heroes over the edge. On such a graph the hero
        /// walks straight (the local navigator still avoids walls and, when fleeing, cliffs).
        /// </summary>
        private static void DropStubNodeGraph(BroadNavigationSystem.Agent agent)
        {
            if (!(agent.system is NodeGraphNavigationSystem)) return;
            var nav = new NodeGraphNavigationSystem.Agent(agent);
            NodeGraph graph = nav.nodeGraph;
            if (graph && graph.GetNodeCount() < StubGraphNodes) nav.nodeGraph = null;
        }

        private BrainMode CurrentMode => brain ? brain.Mode : BrainMode.Idle;

        private void UpdateAI(float dt)
        {
            bodyInputs.moveVector = Vector3.zero;
            dominantDriver = null;
            driverMeetsConditions = false;
            driverSlot = SkillSlot.None;
            aimConfirmationNeeded = false;
            sprintInFight = false;
            pathJumps = false;
            fallbackFire = false;
            if (!body || !bodyInputBank) return;

            gate.Track(bodySkillLocator);
            BrainMode mode = CurrentMode;
            switch (mode)
            {
                case BrainMode.Go:
                    UpdateGo(dt);
                    break;
                case BrainMode.Fight:
                    UpdateFight(dt);
                    break;
                // Cast and Idle stand still: a cast aims and presses in GenerateBodyInputs
            }
            if (mode != BrainMode.Go)
            {
                stuck.Reset(footPosition);
                if (brain) brain.Stuck = false;
            }
        }

        // ---- Go: travel to a point ----------------------------------------------------------------------------------

        private void UpdateGo(float dt)
        {
            Vector3 goal = brain.Goal;
            ai.SetGoalPosition(goal);

            // A fleeing hero never steps off an edge: a fall leaves it at 1 HP, usually among the enemies it ran from.
            // An ordered move may (the path to a clicked point can lead down a ledge).
            bool fleeing = brain.Fleeing;
            BroadNavigationSystem.AgentOutput output = ai.broadNavigationAgent.output;
            Vector3 next = output.nextPosition ?? footPosition;
            // unreachable goal: head straight at it, the local navigator stops at walls (and at edges: only a real path
            // may lead down a ledge, a straight line into the unknown may not)
            bool straight = !fleeing && !output.nextPosition.HasValue && !output.targetReachable;
            if (straight) next = goal;

            // A goal close by, in sight and on this level is walked to straight: the path ends at the node nearest to
            // the goal, which may lie beside it (a hold spot on a rock, a slot by a wall) and lead the hero away.
            Vector3 flat = goal - footPosition;
            flat.y = 0f;
            bool near = flat.sqrMagnitude < DirectGoal * DirectGoal && Mathf.Abs(goal.y - footPosition.y) < DirectGoalClimb
                        && Perception.HasLineOfSight(footPosition + Vector3.up, goal + Vector3.up);
            if (near) next = goal;
            Vector3? sidestep = stuck.Update(footPosition, goal, flat.sqrMagnitude > ArrivedSlack * ArrivedSlack, ai, body, characterMaster.name);
            brain.Stuck = stuck.Stuck;
            if (sidestep.HasValue) next = footPosition + sidestep.Value * SidestepLength;

            ai.localNavigator.targetPosition = next + (bodyTransform.position - footPosition);
            ai.localNavigator.allowWalkOffCliff = !fleeing && !straight && !near && !sidestep.HasValue;
            ai.localNavigator.Update(dt);
            bodyInputs.moveVector = ai.localNavigator.moveVector;
            pathJumps = !sidestep.HasValue && !near && !straight;
        }

        // ---- Fight: the game's skill drivers, our walking -----------------------------------------------------------------

        private void UpdateFight(float dt)
        {
            CharacterBody enemy = brain.Enemy;
            BaseAI.SkillDriverEvaluation eval = ai.skillDriverEvaluation;
            dominantDriver = eval.dominantSkillDriver;
            BaseAI.Target target = eval.target;

            if (freshDriver)
            {
                freshDriver = false;
                hasSkillButtonBeenUnheld = false;
                hasSkillButtonBeenHeld = false;
            }

            if (dominantDriver)
            {
                driverSlot = dominantDriver.skillSlot;
                ai.aimVectorDampTimeOverride = dominantDriver.aimVectorDampTimeOverride;
                ai.aimVectorMaxSpeedOverride = dominantDriver.aimVectorMaxSpeedOverride;
                driverMeetsConditions = target != null && target.gameObject && ActivationConditionsMet(eval);
            }
            else
            {
                ai.aimVectorDampTimeOverride = -1f;
                ai.aimVectorMaxSpeedOverride = -1f;
            }
            if (!enemy) return;

            // the path is always to the enemy: the walking below may use its next point
            BaseAI.Target current = ai.currentEnemy;
            bool tracked = current.gameObject == enemy.gameObject;
            if (tracked) ai.SetGoalPosition(current);
            else ai.SetGoalPosition(enemy.footPosition);
            bool sight = tracked ? current.hasLoS : Perception.HasLineOfSight(bodyInputBank.aimOrigin, enemy.corePosition);

            bool presses = dominantDriver && driverMeetsConditions && DriverAllowed(dominantDriver);
            fallbackFire = !presses && WantsFallbackFire(enemy);
            BroadNavigationSystem.AgentOutput output = ai.broadNavigationAgent.output;
            CombatMover.Plan plan = mover.Decide(brain, body, footPosition, enemy, sight, output, dominantDriver, presses);
            if (plan.Stand) return;

            // the local navigator has no notion of "arrived": a point under the hero's feet makes it run on the spot,
            // overshoot and drift (out of a hold circle, for one)
            Vector3 step = plan.Point - footPosition;
            step.y = 0f;
            if (step.sqrMagnitude < StopDistance * StopDistance) return;

            pathJumps = plan.FollowsPath;
            ai.localNavigator.targetPosition = plan.Point + (bodyTransform.position - footPosition);
            ai.localNavigator.allowWalkOffCliff = plan.AllowWalkOffCliff;
            ai.localNavigator.Update(dt);
            if (ai.localNavigator.wasObstructedLastUpdate) mover.ObstacleHit();
            bodyInputs.moveVector = ai.localNavigator.moveVector;

            sprintInFight = plan.Approaching && (dominantDriver
                ? dominantDriver.shouldSprint
                : (enemy.footPosition - footPosition).sqrMagnitude > SprintToFarTarget * SprintToFarTarget);
        }

        // The survivors' drivers leave gaps: Commando's gun is allowed only from 8 to 19 m (closer, the Umbra is meant to
        // back off - a walking-only rule we do not take), a target that came into sight after the last evaluation waits for
        // the next one. A seen enemy within the primary's reach with no rule pressing anything gets the primary anyway.
        private bool WantsFallbackFire(CharacterBody enemy)
        {
            if (!brain.AutoSlot[0] || !gate.Allows(brain, 0, null, body)) return false;
            if (dominantDriver && dominantDriver.movementType == AISkillDriver.MovementType.Stop && driverMeetsConditions) return false; // a channel
            float reach = brain.Melee ? MeleeFallbackReach : brain.AttackRange;
            return Perception.DriverDistance(body, enemy) <= reach && Perception.HasLineOfSight(bodyInputBank.aimOrigin, enemy.corePosition);
        }

        // line of sight is tested right now, not taken from the cached value
        private bool ActivationConditionsMet(in BaseAI.SkillDriverEvaluation eval)
        {
            bool targetLos = dominantDriver.activationRequiresTargetLoS;
            bool aimLos = dominantDriver.activationRequiresAimTargetLoS;
            aimConfirmationNeeded = dominantDriver.activationRequiresAimConfirmation;
            if (eval.target == eval.aimTarget && targetLos && aimLos) aimLos = false;
            if (targetLos && !eval.target.TestLOSNow()) return false;
            if (aimLos && eval.aimTarget != null && !eval.aimTarget.TestLOSNow()) return false;
            return true;
        }

        // ---- inputs ----------------------------------------------------------------------------------------------------

        public override BaseAI.BodyInputs GenerateBodyInputs(in BaseAI.BodyInputs previous)
        {
            BrainMode mode = CurrentMode;

            // a cast that was cut short by another order must not leak its timer into the next one
            if (mode != BrainMode.Cast && (castPressTime >= 0f || castFrames != 0))
            {
                castPressTime = -1f;
                castFrames = 0;
            }
            if (mode != BrainMode.Go) escapeFrames = 0;

            bodyInputs.pressSkill1 = false;
            bodyInputs.pressSkill2 = false;
            bodyInputs.pressSkill3 = false;
            bodyInputs.pressSkill4 = false;
            bodyInputs.pressSprint = false;
            bodyInputs.pressJump = false;
            bodyInputs.pressActivateEquipment = false;
            bodyInputs.desiredAimDirection = Vector3.zero;

            switch (mode)
            {
                case BrainMode.Cast: GenerateCastInputs(previous); break;
                case BrainMode.Go: GenerateTravelInputs(); break;
                case BrainMode.Fight: GenerateFightInputs(previous); break;
            }
            if ((mode == BrainMode.Fight || mode == BrainMode.Go) && body && bodyInputBank)
                equipment.Apply(brain, body, bodyInputBank.aimOrigin, bodyInputBank.aimDirection, previous, ref bodyInputs);

            // The game's path jumps (and its "jump when frustrated") take over the move vector towards the path's next
            // point: right while the hero walks that path, wrong while it strafes, stands on a hold spot or goes straight
            // at a target - there they pulled heroes out of their circle and off cliffs.
            if (pathJumps) ModifyInputsForJumpIfNeccessary(ref bodyInputs);
            else isInJump = false;
            if (stuck.WantsJump) bodyInputs.pressJump = true;
            return bodyInputs;
        }

        private void GenerateTravelInputs()
        {
            bodyInputs.pressSprint = brain && brain.Sprint;
            if (bodyInputs.moveVector != Vector3.zero) AimInDirection(ref bodyInputs, bodyInputs.moveVector); // look where we go
            TryEscapeSkill();
        }

        // A hero running from enemies close by (fleeing, or ordered to retreat) uses its "get away" skill (profile
        // fleeSlot): the move vector and the aim both point away (dashes follow one or the other, depending on the
        // survivor), held for a few frames.
        private void TryEscapeSkill()
        {
            if (!brain) { escapeFrames = 0; return; }
            int slot = brain.Profile.fleeSlot;
            if (slot < 0 || slot > 3 || !brain.AutoSlot[slot]) { escapeFrames = 0; return; }

            if (escapeFrames > 0)
            {
                escapeFrames--;
                SetPress(slot, true);
                return;
            }
            if (!brain.Escaping || brain.EnemiesNear == 0 || bodyInputs.moveVector == Vector3.zero) return;

            GenericSkill skill = GetSkill(slot);
            if (!skill || !skill.IsReady()) return;

            escapeFrames = EscapeFrames;
            SetPress(slot, true);
            if (Settings.VerboseLog.Value)
                Plugin.Log.LogInfo($"{characterMaster.name}: escape skill {skill.skillDef.skillName} ({brain.EnemiesNear} enemies near)");
        }

        private void GenerateFightInputs(in BaseAI.BodyInputs previous)
        {
            bool previousHeld = false;
            switch (driverSlot)
            {
                case SkillSlot.Primary: previousHeld = previous.pressSkill1; break;
                case SkillSlot.Secondary: previousHeld = previous.pressSkill2; break;
                case SkillSlot.Utility: previousHeld = previous.pressSkill3; break;
                case SkillSlot.Special: previousHeld = previous.pressSkill4; break;
            }
            hasSkillButtonBeenUnheld |= !previousHeld;

            bool press = false;
            if (dominantDriver)
            {
                // the gate is asked again: the situation may have changed since the driver was chosen
                press = driverMeetsConditions && DriverAllowed(dominantDriver);
                if (aimConfirmationNeeded && !ai.hasAimConfirmation) press = false;

                switch (dominantDriver.buttonPressType)
                {
                    case AISkillDriver.ButtonPressType.Abstain:
                        press = false;
                        break;
                    case AISkillDriver.ButtonPressType.TapContinuous:
                        press = press && !previousHeld;
                        break;
                    case AISkillDriver.ButtonPressType.PressOnce:
                        press &= !hasSkillButtonBeenHeld;
                        break;
                }
                if (dominantDriver.buttonEnsureReleasedForOneSample) press &= hasSkillButtonBeenUnheld;
            }
            hasSkillButtonBeenHeld |= press;
            if (driverSlot != SkillSlot.None) SetPress((int)driverSlot, press);

            bodyInputs.pressSprint = sprintInFight;
            if (dominantDriver)
            {
                bodyInputs.pressJump |= dominantDriver.shouldJump && !previous.pressJump;
                BaseAI.Target aimTarget = ai.skillDriverEvaluation.aimTarget;
                if (dominantDriver.aimType == AISkillDriver.AimType.MoveDirection) AimInDirection(ref bodyInputs, bodyInputs.moveVector);
                if (aimTarget != null) AimAt(ref bodyInputs, aimTarget);
            }
            else if (brain && brain.Enemy)
            {
                // no rule fits this moment: keep facing the enemy, ready for the next one
                Vector3 direction = brain.Enemy.corePosition - bodyInputBank.aimOrigin;
                if (direction.sqrMagnitude > 0.01f) bodyInputs.desiredAimDirection = direction.normalized;
            }

            if (fallbackFire && !press && brain && brain.Enemy)
            {
                Vector3 direction = brain.Enemy.corePosition - bodyInputBank.aimOrigin;
                if (direction.sqrMagnitude > 0.01f) bodyInputs.desiredAimDirection = direction.normalized;
                bodyInputs.pressSkill1 = Time.frameCount % FallbackPulseFrames < FallbackHeldFrames;
                bodyInputs.pressSprint = false; // most survivors cannot shoot while sprinting
            }
        }

        private void GenerateCastInputs(in BaseAI.BodyInputs previous)
        {
            if (!brain) return;

            // aim at the enemy or the ground point
            Vector3 aimPoint = brain.CastTarget ? brain.CastTarget.corePosition : brain.CastPoint;
            Vector3 direction = aimPoint - bodyInputBank.aimOrigin;
            if (direction.sqrMagnitude > 0.01f) bodyInputs.desiredAimDirection = direction.normalized;

            int slot = brain.CastSlot;
            if (slot == 4)
            {
                bodyInputs.pressActivateEquipment = !previous.pressActivateEquipment;
                brain.NotifyCastPressed();
                return;
            }

            GenericSkill skill = GetSkill(slot);
            if (!skill)
            {
                if (Settings.VerboseLog.Value) Plugin.Log.LogInfo($"{characterMaster.name}: cast slot {slot}: no such skill");
                brain.NotifyCastPressed();
                return;
            }

            // wait a moment so the aim has swung round, then hold the button for a few frames
            if (castPressTime < 0f) castPressTime = Time.time + CastAimSeconds;
            if (Time.time < castPressTime) return;

            // The check is for the start only: pressing the button spends the stock at once, so a skill that is
            // already being cast reports "not ready" while the button is still held.
            if (castFrames == 0 && !skill.IsReady())
            {
                if (Settings.VerboseLog.Value)
                    Plugin.Log.LogInfo($"{characterMaster.name}: cast slot {slot}: not ready (stock {skill.stock}, cooldown {skill.cooldownRemaining:0.0})");
                castPressTime = -1f;
                castFrames = 0;
                brain.NotifyCastPressed();
                return;
            }
            if (castFrames == 0 && Settings.VerboseLog.Value)
                Plugin.Log.LogInfo($"{characterMaster.name}: casting slot {slot} ({skill.skillDef.skillName}) at {aimPoint:0}");

            SetPress(slot, true);
            castFrames++;
            if (castFrames >= CastFrames)
            {
                castPressTime = -1f;
                castFrames = 0;
                brain.NotifyCastPressed();
            }
        }

        private void SetPress(int slot, bool value)
        {
            switch (slot)
            {
                case 0: bodyInputs.pressSkill1 = value; break;
                case 1: bodyInputs.pressSkill2 = value; break;
                case 2: bodyInputs.pressSkill3 = value; break;
                case 3: bodyInputs.pressSkill4 = value; break;
            }
        }

        private GenericSkill GetSkill(int slot)
        {
            if (!bodySkillLocator) return null;
            switch (slot)
            {
                case 0: return bodySkillLocator.primary;
                case 1: return bodySkillLocator.secondary;
                case 2: return bodySkillLocator.utility;
                case 3: return bodySkillLocator.special;
                default: return null;
            }
        }
    }
}
