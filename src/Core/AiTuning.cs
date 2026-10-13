using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace HighCommand
{
    /// <summary>
    /// Tunables of the heroes' AI, section [AI] of the config. The defaults come from the hc_ai measurements of the
    /// scenario tools\qa\aibase.txt; every number has an allowed range, so a typo cannot break the mode.
    /// Per-survivor behaviour (ranges, flee thresholds, skill rules) lives in profiles\hero_profiles.json instead.
    /// </summary>
    internal static class AiTuning
    {
        internal static ConfigEntry<float> ThinkInterval;
        internal static ConfigEntry<float> HoldRadius;
        internal static ConfigEntry<float> AttackMoveRange;
        internal static ConfigEntry<float> AttackMoveLeash;
        internal static ConfigEntry<float> AssistRange;
        internal static ConfigEntry<float> FocusFireWeight;
        internal static ConfigEntry<float> UnreachableSeconds;
        internal static ConfigEntry<float> BurstHoardSeconds;
        internal static ConfigEntry<float> FleeMaxSeconds;
        internal static ConfigEntry<float> FleeCooldownSeconds;
        internal static ConfigEntry<float> StuckSeconds;
        internal static ConfigEntry<bool> UnstuckTeleport;
        internal static ConfigEntry<float> TeleportAfterSeconds;
        internal static ConfigEntry<bool> AutoEquipment;
        internal static ConfigEntry<string> EquipmentInCombat, EquipmentWhenHurt, EquipmentVsBoss;

        internal static void Bind(ConfigFile cfg)
        {
            ThinkInterval = F(cfg, "ThinkInterval", 0.1f, 0.05f, 0.5f,
                "Seconds between two decisions of a hero (target, order, retreat). Lower = quicker reactions, a little more CPU.");
            HoldRadius = F(cfg, "HoldRadius", 4f, 0f, 15f,
                "Hold position: how far a hero may step from its spot to hit an enemy (melee heroes). Pushed farther, it walks back. Skills that move the hero are not used.");
            AttackMoveRange = F(cfg, "AttackMoveRange", 40f, 10f, 100f,
                "Attack-move and patrol: enemies this close to a marching hero are fought.");
            AttackMoveLeash = F(cfg, "AttackMoveLeash", 30f, 5f, 100f,
                "Attack-move and patrol: how far a hero may chase from the place where it met the enemy; then it goes on.");
            AssistRange = F(cfg, "AssistRange", 45f, 0f, 150f,
                "A hero without orders joins the fights of its friends this close: monsters already fighting the army count even when this hero does not see them.");
            FocusFireWeight = F(cfg, "FocusFireWeight", 1f, 0f, 3f,
                "How much heroes prefer the enemy their friends already fire at (focus fire). 0 = every hero picks its own target.");
            UnreachableSeconds = F(cfg, "UnreachableSeconds", 4f, 1f, 20f,
                "A hero that neither hits nor gets closer to its target for this long gives it up for a while (a flier over a pit, a monster on a ledge) and takes another one.");
            BurstHoardSeconds = F(cfg, "BurstHoardSeconds", 8f, 0f, 60f,
                "Skills with the profile rule 'burst' wait for a group or a big target, but not longer than this many seconds of a fight.");
            FleeMaxSeconds = F(cfg, "FleeMaxSeconds", 15f, 3f, 60f,
                "A hurt hero runs from a fight at most this long, then fights again whatever its health.");
            FleeCooldownSeconds = F(cfg, "FleeCooldownSeconds", 6f, 0f, 60f,
                "After running away for the longest allowed time a hero does not run again for this many seconds.");
            StuckSeconds = F(cfg, "StuckSeconds", 2f, 0.5f, 10f,
                "A walking hero that gets nowhere for this long is stuck: it jumps, looks for a new way, then sidesteps.");
            UnstuckTeleport = cfg.Bind("AI", "UnstuckTeleport", true,
                "The last resort for a stuck hero: a short hop to walkable ground further along its way.");
            TeleportAfterSeconds = F(cfg, "TeleportAfterSeconds", 5f, 2f, 30f,
                "Seconds of being stuck before that hop.");
            AutoEquipment = cfg.Bind("AI", "AutoEquipment", true,
                "Heroes use their equipment by themselves: the pieces listed in EquipmentInCombat in a fight, EquipmentWhenHurt when badly hurt, EquipmentVsBoss against a boss.");
            EquipmentInCombat = cfg.Bind("AI", "EquipmentInCombat",
                "CommandMissile,Saw,Blackhole,BFG,Lightning,CritOnUse,DroneBackup,TeamWarCry,CrippleWard,LifestealOnHit,DeathProjectile,Molotov,GummyClone",
                "Equipment (internal names, comma separated) a hero uses on its own when it fights.");
            EquipmentWhenHurt = cfg.Bind("AI", "EquipmentWhenHurt", "Fruit,PassiveHealing,GainArmor",
                "Equipment a hero uses on its own when it is below half health in a fight.");
            EquipmentVsBoss = cfg.Bind("AI", "EquipmentVsBoss", "BossHunter",
                "Equipment a hero uses on its own only against a boss.");
        }

        private static ConfigEntry<float> F(ConfigFile cfg, string key, float value, float min, float max, string description) =>
            cfg.Bind("AI", key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

        // ---- equipment lists (parsed once per change of the text) ----------------------------------------------

        private sealed class NameSet
        {
            private string source;
            private readonly HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            internal bool Contains(ConfigEntry<string> entry, string name)
            {
                string raw = entry != null ? entry.Value ?? string.Empty : string.Empty;
                if (!ReferenceEquals(raw, source))
                {
                    source = raw;
                    names.Clear();
                    foreach (string part in raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string n = part.Trim();
                        if (n.Length > 0) names.Add(n);
                    }
                }
                return name != null && names.Contains(name);
            }
        }

        private static readonly NameSet InCombat = new NameSet(), WhenHurt = new NameSet(), VsBoss = new NameSet();

        internal static bool UseInCombat(string equipmentName) => InCombat.Contains(EquipmentInCombat, equipmentName);
        internal static bool UseWhenHurt(string equipmentName) => WhenHurt.Contains(EquipmentWhenHurt, equipmentName);
        internal static bool UseVsBoss(string equipmentName) => VsBoss.Contains(EquipmentVsBoss, equipmentName);
    }
}
