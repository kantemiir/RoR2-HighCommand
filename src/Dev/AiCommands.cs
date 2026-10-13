using System.Collections.Generic;
using HighCommand.Army;
using HighCommand.Mode;
using HighCommand.Orders;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace HighCommand.Dev
{
    /// <summary>Console commands for testing the heroes' AI (host only): measurements and orders without the interface.</summary>
    internal static class AiCommands
    {
        private const ConVarFlags HostOnly = ConVarFlags.None;

        [ConCommand(commandName = "hc_ai", flags = HostOnly,
            helpText = "hc_ai [reset | report | trace on|off]: measures the heroes' behaviour (passive time near enemies, modes, skills, damage, orders).")]
        private static void CcAi(ConCommandArgs args)
        {
            AiProbe probe = AiProbe.Ensure();
            if (!probe) throw new ConCommandException("Not in a High Command run.");
            string what = args.Count > 0 ? args.GetArgString(0) : "report";
            switch (what)
            {
                case "reset":
                    probe.ResetStats();
                    Debug.Log("HighCommand: AI measurements reset.");
                    break;
                case "trace":
                    bool on = args.Count < 2 || args.GetArgString(1) != "off";
                    probe.SetTrace(on);
                    Debug.Log("HighCommand: AI trace " + (on ? "on" : "off"));
                    break;
                default:
                    Debug.Log(probe.Report());
                    break;
            }
        }

        [ConCommand(commandName = "hc_order", flags = HostOnly,
            helpText = "hc_order <move|amove|hold|stop|retreat|patrol|seek|attack|attackbig|follow> [dx dz] [heroId ...]: gives an order to the army (or the listed heroes); points are relative to the army's centre.")]
        private static void CcOrder(ConCommandArgs args)
        {
            HighCommandRun run = HighCommandRun.Instance;
            if (!NetworkServer.active || run == null) throw new ConCommandException("Not in a High Command run.");
            if (args.Count < 1) throw new ConCommandException("Usage: hc_order <kind> [dx dz] [heroId ...]");

            string kind = args.GetArgString(0).ToLowerInvariant();
            int next = 1;
            Vector3 offset = Vector3.zero;
            if (args.Count >= 3 && float.TryParse(args.GetArgString(1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float dx)
                && float.TryParse(args.GetArgString(2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float dz))
            {
                offset = new Vector3(dx, 0f, dz);
                next = 3;
            }

            var ids = new List<int>();
            for (int i = next; i < args.Count; i++) ids.Add(args.GetArgInt(i));
            if (ids.Count == 0)
                foreach (Hero h in run.Army.Heroes) if (h.IsAlive) ids.Add(h.Id);

            Vector3 center = ArmyCenter(run, ids);
            var command = new OrderCommand { HeroIds = ids.ToArray(), Point = Formation.SnapToGround(center + offset) };
            switch (kind)
            {
                case "move": command.Kind = OrderKind.Move; break;
                case "amove": command.Kind = OrderKind.AttackMove; break;
                case "hold": command.Kind = OrderKind.Hold; break;
                case "stop": command.Kind = OrderKind.Stop; break;
                case "retreat": command.Kind = OrderKind.Retreat; command.Point = run.RallyPoint; break;
                case "patrol": command.Kind = OrderKind.Patrol; break;
                case "seek": command.Kind = OrderKind.SeekEnemies; break;
                case "attack":
                case "attackbig":
                {
                    CharacterBody enemy = PickEnemy(center, kind == "attackbig");
                    if (!enemy) throw new ConCommandException("No enemy to attack.");
                    command.Kind = OrderKind.Attack;
                    command.Focus = true;
                    NetworkIdentity id = enemy.GetComponent<NetworkIdentity>();
                    command.TargetId = id ? id.netId : NetworkInstanceId.Invalid;
                    command.Point = enemy.corePosition;
                    break;
                }
                case "follow":
                {
                    Hero leader = run.Army.Heroes.Count > 0 ? run.Army.Heroes[0] : null;
                    NetworkIdentity id = leader != null && leader.Body ? leader.Body.GetComponent<NetworkIdentity>() : null;
                    if (!id) throw new ConCommandException("No leader to follow.");
                    command.Kind = OrderKind.Follow;
                    command.TargetId = id.netId;
                    ids.Remove(leader.Id);
                    command.HeroIds = ids.ToArray();
                    break;
                }
                default:
                    throw new ConCommandException("Unknown order: " + kind);
            }
            OrderService.Execute(command);
            Debug.Log($"HighCommand: order {command.Kind} -> [{string.Join(",", command.HeroIds)}] at {command.Point:0} (centre {center:0})");
        }

        [ConCommand(commandName = "hc_spawn_at", flags = HostOnly,
            helpText = "hc_spawn_at <count> <MasterName> <dx> <dz> [elite]: spawns hostile AI in a group around a point relative to the army's centre.")]
        private static void CcSpawnAt(ConCommandArgs args)
        {
            HighCommandRun run = HighCommandRun.Instance;
            if (!NetworkServer.active || run == null) throw new ConCommandException("Not in a High Command run.");
            int count = Mathf.Clamp(args.GetArgInt(0), 1, 100);
            GameObject prefab = MasterCatalog.FindMasterPrefab(args.GetArgString(1));
            if (!prefab) throw new ConCommandException("Unknown master: " + args.GetArgString(1));
            var all = new List<int>();
            foreach (Hero h in run.Army.Heroes) if (h.IsAlive) all.Add(h.Id);
            Vector3 at = ArmyCenter(run, all) + new Vector3(args.GetArgFloat(2), 0f, args.GetArgFloat(3));
            bool elite = args.Count > 4 && args.GetArgString(4) == "elite";

            int made = 0;
            for (int i = 0; i < count; i++)
            {
                Vector2 jitter = Random.insideUnitCircle * 6f;
                var summon = new MasterSummon
                {
                    masterPrefab = prefab,
                    position = Formation.SnapToGround(at + new Vector3(jitter.x, 0f, jitter.y)),
                    rotation = Quaternion.identity,
                    teamIndexOverride = TeamIndex.Monster,
                    ignoreTeamMemberLimit = true,
                    useAmbientLevel = true
                };
                CharacterMaster m = summon.Perform();
                if (!m) continue;
                made++;
                if (elite && m.inventory)
                {
                    EquipmentLocation slot = m.inventory.activeEquipmentLocation;
                    m.inventory.SetEquipmentIndex(RoR2Content.Equipment.AffixRed.equipmentIndex, in slot);
                }
            }
            Debug.Log($"HighCommand: spawned {made}/{count} x {args.GetArgString(1)} at {at:0}.");
        }

        private static Vector3 ArmyCenter(HighCommandRun run, List<int> ids)
        {
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (Hero h in run.Army.Heroes)
            {
                if (!h.IsAlive || !ids.Contains(h.Id)) continue;
                sum += h.Body.footPosition;
                n++;
            }
            return n > 0 ? sum / n : run.RallyPoint;
        }

        private static CharacterBody PickEnemy(Vector3 center, bool biggest)
        {
            CharacterBody best = null;
            float bestScore = float.MaxValue;
            foreach (TeamIndex team in new[] { TeamIndex.Monster, TeamIndex.Lunar, TeamIndex.Void })
            {
                foreach (TeamComponent tc in TeamComponent.GetTeamMembers(team))
                {
                    CharacterBody b = tc ? tc.body : null;
                    if (!b || !b.healthComponent || !b.healthComponent.alive) continue;
                    float score = biggest ? -b.healthComponent.fullCombinedHealth : (b.corePosition - center).sqrMagnitude;
                    if (score < bestScore) { bestScore = score; best = b; }
                }
            }
            return best;
        }
    }
}
