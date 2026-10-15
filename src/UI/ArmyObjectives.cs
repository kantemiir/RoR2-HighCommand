using System.Collections.Generic;
using HighCommand.Army;
using HighCommand.Mode;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace HighCommand.UI
{
    /// <summary>
    /// The objectives of the stock objective panel, told for an army: the game's own lines for the teleporter (find it,
    /// charge it, defeat the boss, go through) with the key that does it, the charge counted for the heroes in the zone
    /// rather than for the commander's (missing) body, and the chests of the stage as a side objective. Every other stock
    /// objective (beacons, the arena, the moon ...) stays as it is.
    /// </summary>
    internal static class ArmyObjectives
    {
        private const float CountEvery = 0.5f;   // the panel asks every frame: counting the map is done this often

        private static bool hooked;
        private static float countedAt = -10f;
        private static int chestsOpened, chestsTotal;

        internal static void Hook()
        {
            if (hooked) return;
            hooked = true;
            ObjectivePanelController.collectObjectiveSources += Collect;
        }

        internal static void Unhook()
        {
            if (!hooked) return;
            hooked = false;
            ObjectivePanelController.collectObjectiveSources -= Collect;
        }

        internal static void Chests(out int opened, out int total)
        {
            float now = Time.unscaledTime;
            if (now - countedAt >= CountEvery)
            {
                countedAt = now;
                Interactables.CountChests(out chestsOpened, out chestsTotal);
            }
            opened = chestsOpened;
            total = chestsTotal;
        }

        /// <summary>Heroes alive, and how many of them stand in a holdout zone.</summary>
        internal static void InZone(HoldoutZoneController zone, out int inside, out int alive)
        {
            inside = alive = 0;
            HcState state = HcState.Instance;
            if (state == null || !zone) return;
            var heroes = state.Heroes;
            for (int i = 0; i < heroes.Count; i++)
            {
                CharacterBody body = heroes[i].Body;
                if (!body || !heroes[i].IsAlive) continue;
                alive++;
                if (zone.IsBodyInChargingRadius(body)) inside++;
            }
        }

        private static void Collect(CharacterMaster master, List<ObjectivePanelController.ObjectiveSourceDescriptor> output)
        {
            if (!HighCommandRun.IsActive) return;
            for (int i = output.Count - 1; i >= 0; i--)
            {
                string kind = output[i].objectiveType != null ? output[i].objectiveType.Name : "";
                if (kind == "FindTeleporterObjectiveTracker" || kind == "FinishTeleporterObjectiveTracker") output.RemoveAt(i);
                else if (kind == "ChargeHoldoutZoneObjectiveTracker")
                {
                    var d = output[i];
                    d.objectiveType = typeof(HoldoutObjective);
                    output[i] = d;
                }
            }

            TeleporterInteraction tp = TeleporterInteraction.instance;
            HcState state = HcState.Instance;
            if (tp && state != null)
            {
                System.Type type = null;
                switch (state.Teleporter)
                {
                    case HcState.TeleState.Idle: type = typeof(FindTeleporterObjective); break;
                    case HcState.TeleState.BossAlive: type = typeof(BossObjective); break;
                    case HcState.TeleState.Charged: type = typeof(ExitObjective); break;
                }
                if (type != null)
                    output.Add(new ObjectivePanelController.ObjectiveSourceDescriptor { source = tp, master = master, objectiveType = type });
            }

            Chests(out int opened, out int total);
            if (total > 0 && opened < total && SceneInfo.instance)
                output.Add(new ObjectivePanelController.ObjectiveSourceDescriptor
                {
                    source = SceneInfo.instance, master = master, objectiveType = typeof(ChestsObjective)
                });
        }

        /// <summary>A second line under an objective: the key in the game's key colour and what it does.</summary>
        internal static string KeyLine(KeyCode key, string token) =>
            "\n<size=85%>" + Tooltip.KeyHint(UiKit.KeyText(key)) + " <style=cSub>" + Loc.Get(token) + "</style></size>";
    }

    // The trackers are created by the stock panel through reflection: public parameterless constructors.

    internal sealed class FindTeleporterObjective : ObjectivePanelController.ObjectiveTracker
    {
        private KeyCode shownKey = KeyCode.None;

        public FindTeleporterObjective()
        {
            baseToken = "OBJECTIVE_FIND_TELEPORTER";
            isPrimary = true;
        }

        protected override bool IsDirty() => cachedString == null || shownKey != Settings.KeyTeleporter.Value;

        protected override string GenerateString()
        {
            shownKey = Settings.KeyTeleporter.Value;
            return Language.GetString(baseToken) + ArmyObjectives.KeyLine(shownKey, "HIGHCOMMAND_OBJ_TP_ACTIVATE");
        }
    }

    internal sealed class BossObjective : ObjectivePanelController.ObjectiveTracker
    {
        public BossObjective()
        {
            baseToken = "OBJECTIVE_DEFEAT_BOSS";
            isPrimary = true;
        }
    }

    internal sealed class ExitObjective : ObjectivePanelController.ObjectiveTracker
    {
        private KeyCode shownKey = KeyCode.None;

        public ExitObjective()
        {
            baseToken = "OBJECTIVE_FINISH_TELEPORTER";
            isPrimary = true;
        }

        protected override bool IsDirty() => cachedString == null || shownKey != Settings.KeyTeleporter.Value;

        protected override string GenerateString()
        {
            shownKey = Settings.KeyTeleporter.Value;
            return Language.GetString(baseToken) + ArmyObjectives.KeyLine(shownKey, "HIGHCOMMAND_OBJ_TP_LEAVE");
        }
    }

    /// <summary>
    /// The charge of a holdout zone (the teleporter, a moon pillar, a void cell): the stock text, red and flashing while
    /// no hero stands in the zone; for the teleporter the heroes inside and the key that gathers them.
    /// </summary>
    internal sealed class HoldoutObjective : ObjectivePanelController.ObjectiveTracker
    {
        private int shownKey = int.MinValue;
        private int percent;
        private int inside, alive;
        private bool empty;

        private HoldoutZoneController Zone => sourceDescriptor.source as HoldoutZoneController;

        public HoldoutObjective()
        {
            isPrimary = true;
        }

        protected override bool IsDirty()
        {
            HoldoutZoneController zone = Zone;
            if (!zone) return cachedString == null;
            percent = zone.displayChargePercent;
            ArmyObjectives.InZone(zone, out inside, out alive);
            empty = inside == 0;
            int blink = empty && (int)(Time.time * 12f) % 2 == 0 ? 1 : 0;
            int key = ((percent * 64 + inside) * 64 + alive) * 2 + blink;
            if (key == shownKey && cachedString != null) return false;
            shownKey = key;
            return true;
        }

        protected override string GenerateString()
        {
            HoldoutZoneController zone = Zone;
            if (!zone) return "";
            string text = string.Format(Language.GetString(empty ? zone.outOfBoundsObjectiveToken : zone.inBoundsObjectiveToken), percent);
            if (empty && (shownKey & 1) == 1) text = "<style=cDeath>" + text + "</style>";
            if (zone.GetComponent<TeleporterInteraction>())
                text += "\n<size=85%><style=cSub>" + Loc.Format("HIGHCOMMAND_OBJ_IN_ZONE", inside, alive) + "</style>  "
                        + Tooltip.KeyHint(UiKit.KeyText(Settings.KeyTeleporter.Value)) + " <style=cSub>"
                        + Loc.Get("HIGHCOMMAND_OBJ_TP_GATHER") + "</style></size>";
            return text;
        }
    }

    /// <summary>The stage's chests, opened / all: a reason to look round the whole map; completes when all are open.</summary>
    internal sealed class ChestsObjective : ObjectivePanelController.ObjectiveTracker
    {
        private int shownOpened = -1, shownTotal = -1;
        private KeyCode shownKey = KeyCode.None;

        protected override bool IsDirty()
        {
            ArmyObjectives.Chests(out int opened, out int total);
            return opened != shownOpened || total != shownTotal || shownKey != Settings.KeySeekChests.Value || cachedString == null;
        }

        protected override string GenerateString()
        {
            ArmyObjectives.Chests(out shownOpened, out shownTotal);
            shownKey = Settings.KeySeekChests.Value;
            return Loc.Format("HIGHCOMMAND_OBJ_CHESTS", shownOpened, shownTotal) + ArmyObjectives.KeyLine(shownKey, "HIGHCOMMAND_OBJ_CHESTS_KEY");
        }
    }
}
