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
        private const float AimSeconds = 0.25f;   // the aim swings round before the press (missiles, the Preon go where it looks)

        private float aimUntil = -1f;

        /// <summary>
        /// Called with the inputs being built: when a listed piece fits the moment, aims at the enemy for a moment and
        /// presses the button once. True while it is aiming or pressing (the caller keeps that aim).
        /// </summary>
        internal bool Apply(HeroBrain brain, CharacterBody self, Vector3 aimOrigin, in BaseAI.BodyInputs previous, ref BaseAI.BodyInputs inputs)
        {
            if (!Wanted(brain, self, out CharacterBody target))
            {
                aimUntil = -1f;
                return false;
            }
            if (target)
            {
                Vector3 direction = target.corePosition - aimOrigin;
                if (direction.sqrMagnitude > 0.01f) inputs.desiredAimDirection = direction.normalized;
            }
            if (aimUntil < 0f) aimUntil = Time.time + (target ? AimSeconds : 0f);
            if (Time.time < aimUntil) return true;

            inputs.pressActivateEquipment = !previous.pressActivateEquipment;
            if (inputs.pressActivateEquipment)
            {
                aimUntil = -1f;
                if (Settings.VerboseLog.Value) Plugin.Log.LogInfo($"{brain.Hero}: uses its equipment {EquipmentName(self)}");
            }
            return true;
        }

        private static bool Wanted(HeroBrain brain, CharacterBody self, out CharacterBody target)
        {
            target = null;
            if (!AiTuning.AutoEquipment.Value || brain == null || !brain.AutoSlot[4] || brain.Profile.RuleFor(4) == HeroProfile.RuleNever) return false;
            EquipmentSlot slot = self ? self.equipmentSlot : null;
            if (!slot || slot.stock <= 0) return false;
            string name = EquipmentName(self);
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

        private static string EquipmentName(CharacterBody self)
        {
            EquipmentSlot slot = self ? self.equipmentSlot : null;
            EquipmentDef def = slot ? EquipmentCatalog.GetEquipmentDef(slot.equipmentIndex) : null;
            return def ? def.name : null;
        }
    }
}
