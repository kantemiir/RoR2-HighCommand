using RoR2;
using RoR2.CharacterAI;
using UnityEngine;

namespace HighCommand.Orders
{
    /// <summary>
    /// Uses a hero's equipment on its own (server; one per body executor). The survivors' AI drivers never press the
    /// equipment button, so without this a hero carries a Royal Capacitor for the whole run. Which pieces are used when
    /// is set in the config (<see cref="AiTuning"/>): combat pieces in a fight, healing pieces when badly hurt, boss pieces
    /// against a boss. Anything not listed (a Recycler, a Fuel Array, a Glowing Meteorite that hits friends too) is left
    /// to the player's cast order.
    /// </summary>
    internal sealed class EquipmentUser
    {
        private const float CombatRange = 40f;
        private const float HurtShare = 0.5f;
        private const float AimTolerance = 8f;    // degrees: missiles, the Preon, the Capacitor go where the hero looks
        private const float MaxAimSeconds = 1f;   // a target the aim cannot follow (it moves too fast) is pressed at anyway
        private const float RetryDelay = 2f;      // a press that did nothing (no target in the cone) is not repeated at once

        private float aimingSince = -1f;
        private float retryAt;
        private int stockBefore = -1;

        /// <summary>
        /// Called with the inputs being built: when a listed piece fits the moment, aims at the enemy until the aim is
        /// on it and presses the button once. True while it is aiming or pressing (the caller keeps that aim).
        /// </summary>
        internal bool Apply(HeroBrain brain, CharacterBody self, Vector3 aimOrigin, Vector3 currentAim, in BaseAI.BodyInputs previous,
            ref BaseAI.BodyInputs inputs)
        {
            float now = Time.time;
            if (now < retryAt || !Wanted(brain, self, out CharacterBody target, out EquipmentSlot slot))
            {
                aimingSince = -1f;
                return false;
            }

            if (aimingSince < 0f)
            {
                aimingSince = now;
                stockBefore = slot.stock;
            }
            if (target)
            {
                Vector3 direction = target.corePosition - aimOrigin;
                if (direction.sqrMagnitude > 0.01f)
                {
                    inputs.desiredAimDirection = direction.normalized;
                    bool onTarget = Vector3.Angle(currentAim, direction) <= AimTolerance;
                    if (!onTarget && now - aimingSince < MaxAimSeconds) return true;
                }
            }

            inputs.pressActivateEquipment = !previous.pressActivateEquipment;
            if (!inputs.pressActivateEquipment) return true;
            aimingSince = -1f;
            retryAt = now + RetryDelay;
            if (Settings.VerboseLog.Value) Plugin.Log.LogInfo($"{brain.Hero}: uses its equipment {EquipmentName(slot)} (stock {stockBefore})");
            return true;
        }

        private bool Wanted(HeroBrain brain, CharacterBody self, out CharacterBody target, out EquipmentSlot slot)
        {
            target = null;
            slot = self ? self.equipmentSlot : null;
            if (!AiTuning.AutoEquipment.Value || brain == null || !brain.AutoSlot[4] || brain.Profile.RuleFor(4) == HeroProfile.RuleNever) return false;
            if (!slot || slot.stock <= 0) return false;
            string name = EquipmentName(slot);
            if (name == null) return false;

            CharacterBody enemy = brain.Mode == BrainMode.Fight ? brain.Enemy : null;
            bool inReach = enemy && (enemy.corePosition - self.corePosition).sqrMagnitude < CombatRange * CombatRange;
            HealthComponent hc = self.healthComponent;
            bool hurt = hc && hc.combinedHealthFraction < HurtShare && (inReach || brain.Fleeing);

            if (hurt && AiTuning.UseWhenHurt(name)) return true;
            if (!inReach) return false;
            target = enemy;
            return AiTuning.UseInCombat(name) || (EnemyTraits.IsBig(enemy) && AiTuning.UseVsBoss(name));
        }

        private EquipmentIndex namedIndex = EquipmentIndex.None;
        private string namedName;

        // Object.name builds a new string on every call: the name of the last piece asked for is kept
        private string EquipmentName(EquipmentSlot slot)
        {
            if (!slot) return null;
            EquipmentIndex index = slot.equipmentIndex;
            if (index != namedIndex)
            {
                EquipmentDef def = EquipmentCatalog.GetEquipmentDef(index);
                namedName = def ? def.name : null;
                namedIndex = index;
            }
            return namedName;
        }
    }
}
