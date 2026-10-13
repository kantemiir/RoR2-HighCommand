using System.Collections.Generic;
using RoR2;
using RoR2.CharacterAI;

namespace HighCommand.Orders
{
    /// <summary>
    /// What kind of fighter an enemy is, as far as target choice cares. Whether it attacks from afar is learned once per
    /// body type from the skill drivers of its own AI (the longest reach of a driver that uses a skill), so modded
    /// monsters are covered too.
    /// </summary>
    internal static class EnemyTraits
    {
        /// <summary>An attack that reaches this far (meters) makes a monster a ranged one.</summary>
        private const float RangedReach = 20f;

        private static readonly Dictionary<BodyIndex, bool> RangedByBody = new Dictionary<BodyIndex, bool>();

        internal static bool IsRanged(CharacterBody body)
        {
            if (!body) return false;
            BodyIndex index = body.bodyIndex;
            if (RangedByBody.TryGetValue(index, out bool ranged)) return ranged;

            BaseAI ai = AiOf(body);
            if (!ai) return false; // a body without its AI yet: ask again later
            ranged = false;
            AISkillDriver[] drivers = ai.skillDrivers;
            for (int i = 0; drivers != null && i < drivers.Length && !ranged; i++)
            {
                AISkillDriver d = drivers[i];
                ranged = d && d.skillSlot != SkillSlot.None && d.buttonPressType != AISkillDriver.ButtonPressType.Abstain
                         && d.maxDistance >= RangedReach;
            }
            RangedByBody[index] = ranged;
            return ranged;
        }

        /// <summary>Mending elites heal the monsters around them.</summary>
        internal static bool IsHealer(CharacterBody body) => body && body.HasBuff(DLC1Content.Buffs.EliteEarth);

        /// <summary>Marked by the commander (the Mark ability): the whole army should fall on it.</summary>
        internal static bool IsMarked(CharacterBody body) => body && body.HasBuff(RoR2Content.Buffs.DeathMark);

        internal static bool IsBig(CharacterBody body) => body && (body.isBoss || body.isChampion);

        /// <summary>The body a monster's AI is fighting right now (null when it has none).</summary>
        internal static CharacterBody TargetOf(CharacterBody enemy)
        {
            BaseAI ai = AiOf(enemy);
            return ai != null ? ai.currentEnemy.characterBody : null;
        }

        private static BaseAI AiOf(CharacterBody body)
        {
            CharacterMaster master = body ? body.master : null;
            if (!master) return null;
            BaseAI[] ais = master.AiComponents;
            return ais != null && ais.Length > 0 ? ais[0] : null;
        }
    }
}
