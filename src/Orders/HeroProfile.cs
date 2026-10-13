using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HighCommand.Orders
{
    /// <summary>
    /// How one kind of hero behaves on its own, as plain data (see <see cref="ProfileStore"/>). The skills themselves
    /// are chosen by the game's own AI rules for the survivor; a profile adds ranges, a flee threshold, a usage rule per
    /// skill slot and which skills carry the hero somewhere.
    ///
    /// Rules: "auto" (the survivor's AI decides), "never", "escape" (only when hurt or fleeing), "engage" (only against
    /// a target that is far away: gap closers), "burst" (when several enemies are close or the target is a boss - or
    /// when the skill has waited for that a whole fight long, see AI/BurstHoardSeconds).
    /// </summary>
    public sealed class HeroProfile
    {
        public const string RuleAuto = "auto", RuleNever = "never", RuleEscape = "escape", RuleEngage = "engage", RuleBurst = "burst";

        public string survivor = "*";        // SurvivorDef.cachedName, "*" = every survivor without an own profile
        public string role = "";             // informative

        public float acquireAggressive = 55f;
        public float acquireDefensive = 32f;
        public float leashAggressive = 60f;
        public float leashDefensive = 42f;

        public float retreatHealth = 0.28f;  // below this fraction, and under attack, the hero runs to its friends
        public float recoverHealth = 0.55f;  // back to the fight above this (or as soon as nobody is after it)

        public string primary = RuleAuto;
        public string secondary = RuleAuto;
        public string utility = RuleAuto;
        public string special = RuleAuto;
        public string equipment = RuleAuto;  // auto | never (which pieces are used when: the AI section of the config)

        public int burstEnemies = 3;         // "burst": at least this many enemies within 20 m
        public float engageDistance = 14f;   // "engage": the target is at least this far away

        /// <summary>
        /// Skill slot (0..3) that carries the hero out of a lost fight (a dash, blink, roll, smoke bomb), pressed while it
        /// flees or retreats, aimed and moving away from the enemies; -1 = none.
        /// </summary>
        public int fleeSlot = -1;

        /// <summary>
        /// Ranged heroes: the distance (m) they keep from the enemy they fight; never more than their attacks reach.
        /// Closer than 60 % of it they back off, otherwise they sidestep while shooting. 0 = melee: straight at the enemy.
        /// </summary>
        public float standoff;

        /// <summary>
        /// Skills that move the hero (a dash, a leap, a grapple), comma separated: slot names (primary, secondary, utility,
        /// special) and/or the skills' own names for a skill of another loadout (FlyUp = the Artificer's Ion Surge).
        /// They are not used on hold, out of a zone or after a target on another level.
        /// </summary>
        public string mobility = "";

        private bool[] mobileSlots;
        private HashSet<string> mobileSkills;

        internal HeroProfile Copy()
        {
            var p = (HeroProfile)MemberwiseClone();
            p.mobileSlots = null;
            p.mobileSkills = null;
            return p;
        }

        internal string RuleFor(int slot)
        {
            switch (slot)
            {
                case 0: return primary;
                case 1: return secondary;
                case 2: return utility;
                case 3: return special;
                default: return equipment;
            }
        }

        /// <param name="skillName">the name of the skill in that slot now (SkillDef.skillName), may be null</param>
        internal bool IsMobility(int slot, string skillName)
        {
            if (mobileSlots == null)
            {
                mobileSlots = new bool[4];
                mobileSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string part in (mobility ?? "").Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int s = SlotOf(part);
                    if (s >= 0) mobileSlots[s] = true;
                    else mobileSkills.Add(part.Trim());
                }
            }
            if (slot >= 0 && slot < 4 && mobileSlots[slot]) return true;
            return skillName != null && mobileSkills.Contains(skillName);
        }

        private static int SlotOf(string name)
        {
            switch (name.Trim().ToLowerInvariant())
            {
                case "primary": return 0;
                case "secondary": return 1;
                case "utility": return 2;
                case "special": return 3;
                default: return -1;
            }
        }

        internal static HeroProfile FromJson(Dictionary<string, object> o)
        {
            string name = MiniJson.String(o, "survivor", "*");
            // a field missing from the file (written by an older version) takes the built-in value for that survivor
            HeroProfile d = ProfileStore.BuiltIn(name) ?? new HeroProfile();
            var p = new HeroProfile
            {
                survivor = name,
                role = MiniJson.String(o, "role", d.role),
                acquireAggressive = MiniJson.Float(o, "acquireAggressive", d.acquireAggressive),
                acquireDefensive = MiniJson.Float(o, "acquireDefensive", d.acquireDefensive),
                leashAggressive = MiniJson.Float(o, "leashAggressive", d.leashAggressive),
                leashDefensive = MiniJson.Float(o, "leashDefensive", d.leashDefensive),
                retreatHealth = MiniJson.Float(o, "retreatHealth", d.retreatHealth),
                recoverHealth = MiniJson.Float(o, "recoverHealth", d.recoverHealth),
                primary = MiniJson.String(o, "primary", d.primary),
                secondary = MiniJson.String(o, "secondary", d.secondary),
                utility = MiniJson.String(o, "utility", d.utility),
                special = MiniJson.String(o, "special", d.special),
                equipment = MiniJson.String(o, "equipment", d.equipment),
                burstEnemies = MiniJson.Int(o, "burstEnemies", d.burstEnemies),
                engageDistance = MiniJson.Float(o, "engageDistance", d.engageDistance),
                fleeSlot = MiniJson.Int(o, "fleeSlot", d.fleeSlot),
                standoff = MiniJson.Float(o, "standoff", d.standoff),
                mobility = MiniJson.String(o, "mobility", d.mobility)
            };
            return p;
        }

        internal string ToJson(string indent)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(indent).Append("{\n");
            void S(string k, string v, bool last = false) =>
                sb.Append(indent).Append("  ").Append(MiniJson.Quote(k)).Append(": ").Append(MiniJson.Quote(v)).Append(last ? "\n" : ",\n");
            void N(string k, float v) =>
                sb.Append(indent).Append("  ").Append(MiniJson.Quote(k)).Append(": ").Append(MiniJson.Number(v)).Append(",\n");
            S("survivor", survivor);
            S("role", role);
            N("acquireAggressive", acquireAggressive);
            N("acquireDefensive", acquireDefensive);
            N("leashAggressive", leashAggressive);
            N("leashDefensive", leashDefensive);
            N("retreatHealth", retreatHealth);
            N("recoverHealth", recoverHealth);
            N("burstEnemies", burstEnemies);
            N("engageDistance", engageDistance);
            N("fleeSlot", fleeSlot);
            N("standoff", standoff);
            S("mobility", mobility);
            S("primary", primary);
            S("secondary", secondary);
            S("utility", utility);
            S("special", special);
            S("equipment", equipment, last: true);
            sb.Append(indent).Append("}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Loads <c>profiles\hero_profiles.json</c> next to the plugin (written with sensible defaults the first time).
    /// A broken file never breaks the game: the built-in defaults are used and the problem is logged. A file of an older
    /// format version is set aside (.old) and replaced: its numbers were tuned for an AI that no longer exists.
    /// </summary>
    internal static class ProfileStore
    {
        internal const int FormatVersion = 2;

        private static readonly Dictionary<string, HeroProfile> ByName = new Dictionary<string, HeroProfile>(StringComparer.OrdinalIgnoreCase);
        private static HeroProfile fallback = new HeroProfile();
        private static bool loaded;

        internal static string FilePath
        {
            get
            {
                string dir = Path.GetDirectoryName(typeof(ProfileStore).Assembly.Location) ?? ".";
                return Path.Combine(Path.Combine(dir, "profiles"), "hero_profiles.json");
            }
        }

        internal static HeroProfile For(string survivorName)
        {
            if (!loaded) Load();
            if (!string.IsNullOrEmpty(survivorName) && ByName.TryGetValue(survivorName, out HeroProfile p)) return p;
            return fallback;
        }

        internal static void Reload()
        {
            loaded = false;
            Load();
        }

        private static void Load()
        {
            loaded = true;
            ByName.Clear();
            fallback = new HeroProfile();

            List<HeroProfile> profiles = null;
            string path = FilePath;
            try
            {
                if (File.Exists(path))
                {
                    var root = MiniJson.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
                    var list = root != null && root.TryGetValue("profiles", out object raw) ? raw as List<object> : null;
                    if (list == null || list.Count == 0) throw new InvalidDataException("no \"profiles\" array in the file");

                    int version = root != null ? MiniJson.Int(root, "version", 1) : 1;
                    if (version < FormatVersion)
                    {
                        File.Copy(path, path + ".old", overwrite: true);
                        Plugin.Log.LogInfo($"Hero profiles: format {version} is older than {FormatVersion}; the file was saved as .old and replaced by the new defaults.");
                    }
                    else
                    {
                        profiles = new List<HeroProfile>();
                        foreach (object item in list)
                        {
                            if (item is Dictionary<string, object> dict) profiles.Add(HeroProfile.FromJson(dict));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Hero profiles: {path} is unreadable ({e.Message}); built-in defaults are used.");
                profiles = null;
            }

            if (profiles == null)
            {
                profiles = Defaults();
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, Serialize(profiles));
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Hero profiles: could not write the default file: " + e.Message);
                }
            }

            foreach (HeroProfile p in profiles)
            {
                if (p == null || string.IsNullOrEmpty(p.survivor)) continue;
                Sanitize(p);
                if (p.survivor == "*") fallback = p;
                else ByName[p.survivor] = p;
            }
            Plugin.Log.LogInfo($"Hero profiles: {ByName.Count} survivor profile(s) + fallback loaded.");
        }

        internal static string Serialize(List<HeroProfile> profiles)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"version\": ").Append(FormatVersion).Append(",\n");
            sb.Append("  // rules per skill slot: auto | never | escape | engage | burst; mobility: slots (or skill names) whose skill moves the hero\n");
            sb.Append("  // standoff: the distance a ranged hero keeps (0 = melee); fleeSlot: the skill used to get away (-1 = none)\n");
            sb.Append("  \"profiles\": [\n");
            for (int i = 0; i < profiles.Count; i++)
            {
                sb.Append(profiles[i].ToJson("    "));
                sb.Append(i < profiles.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            return sb.ToString();
        }

        private static void Sanitize(HeroProfile p)
        {
            p.acquireAggressive = Mathf.Clamp(p.acquireAggressive, 10f, 150f);
            p.acquireDefensive = Mathf.Clamp(p.acquireDefensive, 10f, 150f);
            p.leashAggressive = Mathf.Clamp(p.leashAggressive, 10f, 300f);
            p.leashDefensive = Mathf.Clamp(p.leashDefensive, 10f, 300f);
            p.retreatHealth = Mathf.Clamp(p.retreatHealth, 0f, 0.9f);
            p.recoverHealth = Mathf.Clamp(Mathf.Max(p.recoverHealth, p.retreatHealth + 0.05f), 0.05f, 1f);
            p.burstEnemies = Mathf.Clamp(p.burstEnemies, 1, 20);
            p.engageDistance = Mathf.Clamp(p.engageDistance, 2f, 80f);
            if (p.fleeSlot < 0 || p.fleeSlot > 3) p.fleeSlot = -1;
            p.standoff = Mathf.Clamp(p.standoff, 0f, 60f);
            p.primary = KnownRule(p.primary);
            p.secondary = KnownRule(p.secondary);
            p.utility = KnownRule(p.utility);
            p.special = KnownRule(p.special);
            p.equipment = p.equipment == HeroProfile.RuleNever ? HeroProfile.RuleNever : HeroProfile.RuleAuto;
        }

        private static string KnownRule(string rule)
        {
            switch (rule)
            {
                case HeroProfile.RuleNever:
                case HeroProfile.RuleEscape:
                case HeroProfile.RuleEngage:
                case HeroProfile.RuleBurst:
                    return rule;
                default:
                    return HeroProfile.RuleAuto;
            }
        }

        // ---- built-in defaults -------------------------------------------------------------------------------

        private static Dictionary<string, HeroProfile> builtIn;

        /// <summary>The built-in profile of a survivor (a fresh copy), or null when there is none.</summary>
        internal static HeroProfile BuiltIn(string survivor)
        {
            if (builtIn == null)
            {
                builtIn = new Dictionary<string, HeroProfile>(StringComparer.OrdinalIgnoreCase);
                foreach (HeroProfile p in Defaults()) builtIn[p.survivor] = p;
            }
            return survivor != null && builtIn.TryGetValue(survivor, out HeroProfile found) ? found.Copy() : null;
        }

        private static HeroProfile P(string survivor, string role, float retreat, float standoff = 0f, string mobility = "",
            int fleeSlot = -1, string utility = HeroProfile.RuleAuto, string special = HeroProfile.RuleAuto,
            float acquire = 55f, float leash = 60f)
        {
            return new HeroProfile
            {
                survivor = survivor,
                role = role,
                retreatHealth = retreat,
                recoverHealth = Mathf.Min(0.9f, retreat + 0.3f),
                standoff = standoff,
                mobility = mobility,
                fleeSlot = fleeSlot,
                utility = utility,
                special = special,
                acquireAggressive = acquire,
                leashAggressive = leash
            };
        }

        internal static List<HeroProfile> Defaults()
        {
            const string Utility = "utility";
            const string Escape = HeroProfile.RuleEscape, Engage = HeroProfile.RuleEngage, Burst = HeroProfile.RuleBurst;
            var l = new List<HeroProfile>();
            l.Add(new HeroProfile { survivor = "*", role = "Survivor" });

            // ranged damage: keep their distance, run early, the roll / blink is kept for getting away
            l.Add(P("Commando", "Ranged", 0.3f, standoff: 18f, mobility: Utility, fleeSlot: 2, utility: Escape, special: Burst));
            l.Add(P("Huntress", "Ranged", 0.35f, standoff: 24f, mobility: Utility, fleeSlot: 2, utility: Escape, special: Burst, acquire: 62f));
            l.Add(P("Chef", "Ranged", 0.3f, standoff: 12f, mobility: Utility, utility: Engage, special: Burst));
            l.Add(P("Railgunner", "Sniper", 0.35f, standoff: 30f, utility: Escape, special: Burst, acquire: 70f, leash: 75f));
            l.Add(P("Mage", "Caster", 0.35f, standoff: 18f, mobility: "FlyUp", special: Burst));
            l.Add(P("VoidSurvivor", "Caster", 0.3f, standoff: 14f, mobility: Utility, fleeSlot: 2, utility: Escape, special: Burst));
            l.Add(P("Bandit2", "Assassin", 0.3f, standoff: 10f, fleeSlot: 2, utility: Escape, special: Burst, acquire: 52f));

            // melee and tanks: stay in the fight longer, gap closers open the fight from afar
            l.Add(P("Merc", "Assassin", 0.25f, mobility: "secondary,utility,special", fleeSlot: 2, utility: Engage, special: Burst, acquire: 48f));
            l.Add(P("Loader", "Melee", 0.22f, mobility: "secondary,utility", utility: Engage, special: Burst, acquire: 46f));
            l.Add(P("Croco", "Melee", 0.18f, mobility: Utility, special: Burst, acquire: 42f));
            l.Add(P("Toolbot", "Tank", 0.2f, standoff: 12f, mobility: Utility, acquire: 48f));
            l.Add(P("FalseSon", "Tank", 0.2f, mobility: Utility, utility: Engage, special: Burst, acquire: 46f));
            l.Add(P("Enforcer2", "Tank", 0.2f, standoff: 10f, mobility: "utility,special", acquire: 46f));
            l.Add(P("Drifter", "Melee", 0.22f, mobility: Utility, utility: Engage, special: Burst, acquire: 46f));
            l.Add(P("Acolyte", "Melee", 0.22f, mobility: Utility, utility: Engage, special: Burst, acquire: 46f));

            // support and summoners: behind the line, early retreat
            l.Add(P("Engi", "Summoner", 0.35f, standoff: 18f));
            l.Add(P("DroneTech", "Summoner", 0.35f, standoff: 18f, mobility: Utility));
            l.Add(P("Treebot", "Support", 0.3f, standoff: 14f));
            l.Add(P("Seeker", "Support", 0.3f, standoff: 12f, mobility: Utility));
            l.Add(P("Captain", "Support", 0.3f, standoff: 18f));
            return l;
        }
    }
}
