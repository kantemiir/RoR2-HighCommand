using RoR2;
using RoR2.CharacterAI;
using UnityEngine;

namespace HighCommand.Orders
{
    /// <summary>
    /// Whether a hero may use one of its skills right now (server; one per body executor). The game's skill drivers
    /// pick a skill; this says no when the player switched the skill's auto-use off, when the profile's rule for the slot
    /// does not fit the moment (see <see cref="HeroProfile"/>), or when a skill that carries the hero somewhere would take
    /// it where it must not go (off its hold spot, out of its zone, down a cliff).
    /// </summary>
    internal sealed class SkillGate
    {
        private const int Slots = 4;
        private const float MobilityTetherMargin = 2f;  // a dash may end this far beyond the circle
        private const float MobilityMaxClimb = 6f;      // no dashing after a target this far above or below

        private const float GroundCheckInterval = 0.3f;
        private const float GroundStep = 3f;            // a ray every this many meters of the way
        private const int GroundMaxSteps = 12;
        private const float GroundOvershoot = 5f;       // dashes end a little past their target
        private const float GroundProbeUp = 3f;
        private const float GroundMaxDrop = 6f;         // ground deeper than this below the way is a cliff

        private readonly float[] readySince = { -1f, -1f, -1f, -1f };
        private CharacterBody groundTarget;
        private float groundCheckedAt = -100f;
        private bool groundSafe;

        /// <summary>Notes since when each skill has been ready (a "burst" skill is not hoarded for ever).</summary>
        internal void Track(SkillLocator locator)
        {
            float now = Time.time;
            for (int slot = 0; slot < Slots; slot++)
            {
                GenericSkill skill = locator ? locator.GetSkill((SkillSlot)slot) : null;
                bool ready = skill && skill.IsReady();
                if (!ready) readySince[slot] = -1f;
                else if (readySince[slot] < 0f) readySince[slot] = now;
            }
        }

        /// <summary>True when <paramref name="slot"/> (0 primary .. 3 special) may be used; slots outside 0..3 always may.</summary>
        internal bool Allows(HeroBrain brain, int slot, AISkillDriver driver, CharacterBody self)
        {
            if (slot < 0 || slot >= Slots || brain == null) return true;
            if (!brain.AutoSlot[slot]) return false;

            HeroProfile p = brain.Profile;
            switch (p.RuleFor(slot))
            {
                case HeroProfile.RuleNever:
                    return false;
                case HeroProfile.RuleEscape:
                    if (!brain.LowHealth && !brain.Fleeing) return false;
                    // an escape skill must not be spent by a rule that runs at the enemy (Huntress' blink to close a gap)
                    if (driver && driver.movementType == AISkillDriver.MovementType.ChaseMoveTarget) return false;
                    break;
                case HeroProfile.RuleEngage:
                    if (!brain.Enemy || brain.TargetDistance < p.engageDistance) return false;
                    break;
                case HeroProfile.RuleBurst:
                    if (brain.EnemiesNear < p.burstEnemies && !brain.TargetIsBig && !Hoarded(slot, brain)) return false;
                    break;
            }
            return !IsMobility(p, slot, self) || MobilityAllowed(brain, self);
        }

        internal static bool IsMobility(HeroProfile p, int slot, CharacterBody self)
        {
            GenericSkill skill = self && self.skillLocator ? self.skillLocator.GetSkill((SkillSlot)slot) : null;
            return p.IsMobility(slot, skill && skill.skillDef ? skill.skillDef.skillName : null);
        }

        // A "burst" skill that has waited a whole fight-length for a group or a boss is used anyway.
        private bool Hoarded(int slot, HeroBrain brain) =>
            brain.Mode == BrainMode.Fight && readySince[slot] >= 0f && Time.time - readySince[slot] >= AiTuning.BurstHoardSeconds.Value;

        private bool MobilityAllowed(HeroBrain brain, CharacterBody self)
        {
            if (!brain.MobilitySkills) return false;
            CharacterBody enemy = brain.Enemy;
            if (!enemy || !self) return true;
            if (!brain.Tether.Contains(enemy.footPosition, MobilityTetherMargin)) return false;
            if (Mathf.Abs(enemy.footPosition.y - self.footPosition.y) > MobilityMaxClimb) return false;
            return GroundAllTheWay(self.footPosition, enemy.footPosition, enemy);
        }

        // A dash, a leap or a whirlwind towards a monster at the edge of a cliff carries on past it: there must be
        // ground under the whole way and a little beyond. Tested with a few rays, at most every GroundCheckInterval.
        private bool GroundAllTheWay(Vector3 from, Vector3 to, CharacterBody target)
        {
            float now = Time.time;
            if (target == groundTarget && now - groundCheckedAt < GroundCheckInterval) return groundSafe;
            groundTarget = target;
            groundCheckedAt = now;

            Vector3 way = to - from;
            Vector3 flat = new Vector3(way.x, 0f, way.z);
            float length = flat.magnitude;
            Vector3 end = to + (length > 0.1f ? flat / length * GroundOvershoot : Vector3.zero);
            int steps = Mathf.Clamp(Mathf.CeilToInt((length + GroundOvershoot) / GroundStep), 1, GroundMaxSteps);
            groundSafe = true;
            for (int i = 1; i <= steps && groundSafe; i++)
            {
                Vector3 p = Vector3.Lerp(from, end, i / (float)steps);
                groundSafe = Physics.Raycast(p + Vector3.up * GroundProbeUp, Vector3.down, GroundProbeUp + GroundMaxDrop,
                    LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
            }
            return groundSafe;
        }
    }
}
