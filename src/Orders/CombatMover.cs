using RoR2;
using RoR2.CharacterAI;
using RoR2.Navigation;
using UnityEngine;

namespace HighCommand.Orders
{
    /// <summary>
    /// Where a fighting hero walks (server; one per body executor). The game's skill drivers still choose which skill to
    /// press, but their walking was written for monsters that always close in, fall off cliffs after their prey and know
    /// no hold spot. Here a melee hero goes straight at its target once it sees it close by (the broad path is computed
    /// only every couple of seconds and lags behind a moving target), a ranged hero keeps its fight range, backs off when
    /// rushed and sidesteps while shooting; nobody leaves its tether or walks off a ledge unless the way to the target
    /// really goes down.
    /// </summary>
    internal sealed class CombatMover
    {
        internal struct Plan
        {
            internal Vector3 Point;            // where to walk (ground level)
            internal bool Stand;               // do not walk at all
            internal bool AllowWalkOffCliff;
            internal bool Approaching;         // closing in on the target (sprinting is fine)
            internal bool FollowsPath;         // Point is the broad path's next point (its jumps may be made)
        }

        private const float MeleeContact = 1.5f;     // gap between the two bodies at which a melee hero stops pushing in
        private const float DirectPursuit = 18f;     // closer than this and in sight: straight at the target
        private const float DirectClimb = 2.5f;      // ... if it stands on about the same level
        private const float DropToTarget = 3f;       // a target this far below may be followed down a ledge
        private const float SpreadStep = 1.2f;       // melee heroes on one target come from slightly different sides
        private const float KiteShare = 0.6f;        // a ranged hero backs off when closer than this share of its range
        private const float BackOffStep = 6f;
        private const float BackOffSidestep = 2f;    // backing off a little sideways: no straight line for the monster
        private const float StrafeStep = 4f;
        private const float StrafeMin = 1.2f, StrafeMax = 2.4f; // seconds before a sidestep changes direction
        private const float TetherReturnMargin = 1.5f;
        private const float WalkAway = 10f;          // an escape driver (blink away, leap away) moves this far from the target

        private float strafeSign = 1f;
        private float strafeFlipAt;

        internal void ObstacleHit() => strafeSign = -strafeSign;

        /// <param name="foot">the hero's foot position</param>
        /// <param name="hasSight">the hero sees the target (the AI's own line-of-sight test)</param>
        /// <param name="driver">the skill driver of the moment (may be null)</param>
        /// <param name="driverPresses">the driver's skill is being pressed this update</param>
        internal Plan Decide(HeroBrain brain, CharacterBody self, Vector3 foot, CharacterBody enemy, bool hasSight,
            in BroadNavigationSystem.AgentOutput path, AISkillDriver driver, bool driverPresses)
        {
            var plan = new Plan { Point = foot };
            Vector3 enemyFoot = enemy.footPosition;
            Vector3 to = enemyFoot - foot;
            float dy = to.y;
            to.y = 0f;
            float distance = to.magnitude;
            Vector3 dir = distance > 0.01f ? to / distance : self.transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir);

            if (brain.StandStill) { plan.Stand = true; return plan; }

            // the game's own drivers know two moves the skill depends on: standing still to channel (a snipe, an
            // airstrike), and running away while a blink or a leap fires along the move direction
            if (driver && driverPresses)
            {
                if (driver.movementType == AISkillDriver.MovementType.Stop) { plan.Stand = true; return plan; }
                if (driver.movementType == AISkillDriver.MovementType.FleeMoveTarget && driver.aimType == AISkillDriver.AimType.MoveDirection)
                {
                    plan.Point = brain.Tether.Clamp(foot - dir * WalkAway);
                    return plan;
                }
            }

            Vector3 pathNext = path.nextPosition ?? enemyFoot;
            bool direct = hasSight && distance < DirectPursuit && Mathf.Abs(dy) < DirectClimb;

            if (brain.Melee)
            {
                float contact = MeleeContact + self.radius + enemy.radius;
                if (distance <= contact) plan.Point = enemyFoot;
                else if (direct) plan.Point = enemyFoot + side * Spread(brain);
                else { plan.Point = pathNext; plan.FollowsPath = true; }
                plan.Approaching = distance > contact;
                plan.AllowWalkOffCliff = dy < -DropToTarget && path.targetReachable;
            }
            else
            {
                // measured as the drivers do: from the aim to the target's centre, less the radii
                float range = brain.FightRange;
                float reach = Perception.DriverDistance(self, enemy);
                if (!hasSight || reach > range)
                {
                    plan.Point = direct ? enemyFoot : pathNext;
                    plan.FollowsPath = !direct;
                    plan.Approaching = true;
                }
                else if (reach < range * KiteShare) plan.Point = foot - dir * BackOffStep + side * (strafeSign * BackOffSidestep);
                else plan.Point = foot + side * (strafeSign * StrafeStep);

                if (Time.time >= strafeFlipAt)
                {
                    strafeFlipAt = Time.time + Random.Range(StrafeMin, StrafeMax);
                    strafeSign = -strafeSign;
                }
            }

            Tether tether = brain.Tether;
            if (tether.Active)
            {
                Vector3 inside = tether.Contains(foot, TetherReturnMargin) ? tether.Clamp(plan.Point) : tether.Center;
                if ((inside - plan.Point).sqrMagnitude > 0.01f) plan.FollowsPath = false; // the path's jumps lead out of the circle
                plan.Point = inside;
                plan.AllowWalkOffCliff = false;
            }
            return plan;
        }

        // -1, 0 or +1 steps to the side, by hero: three heroes on one monster do not queue up behind each other
        private static float Spread(HeroBrain brain)
        {
            int id = brain.Hero != null ? brain.Hero.Id : 0;
            return ((id % 3) - 1) * SpreadStep;
        }
    }
}
