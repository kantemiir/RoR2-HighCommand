using System.Collections.Generic;
using RoR2;
using UnityEngine;

namespace HighCommand.Orders
{
    /// <summary>
    /// The "seek enemies" order's hunt across the whole map (server; one per hero): the nearest monster of the map is
    /// the prey; a prey the hero walks after for long without getting closer (a flier over a pit, a ledge) is left alone
    /// for a while and the next one taken.
    /// </summary>
    internal sealed class EnemySeeker
    {
        private const float RescanInterval = 1.5f;
        private const float ProgressMeters = 2f;
        private const float GiveUpSeconds = 10f;
        private const float SkipSeconds = 30f;
        private const float HeightWeight = 2f; // a monster on a ledge far above is a long way round

        private static readonly TeamIndex[] HostileTeams = { TeamIndex.Monster, TeamIndex.Lunar, TeamIndex.Void };

        private readonly Dictionary<CharacterBody, float> skipUntil = new Dictionary<CharacterBody, float>();
        private CharacterBody prey;
        private float scanAt, best, progressAt;

        internal void Reset()
        {
            skipUntil.Clear();
            prey = null;
            scanAt = 0f;
        }

        internal CharacterBody Prey => prey;

        /// <summary>
        /// The monster to walk to (null when the map is empty or the last one turned out to be out of reach).
        /// <paramref name="gaveUp"/> = the current prey was just given up.
        /// </summary>
        internal CharacterBody Update(CharacterBody self, Vector3 pos, out bool gaveUp)
        {
            gaveUp = false;
            TeamIndex own = self.teamComponent.teamIndex;
            bool alive = prey && Perception.IsTargetable(prey, own);
            if (!alive || Time.time >= scanAt)
            {
                scanAt = Time.time + RescanInterval;
                CharacterBody next = Nearest(own, pos);
                if (next != prey)
                {
                    prey = next;
                    best = float.MaxValue;
                    progressAt = Time.time;
                }
            }
            if (!prey) return null;

            float distance = (prey.footPosition - pos).magnitude;
            if (distance < best - ProgressMeters)
            {
                best = distance;
                progressAt = Time.time;
            }
            else if (Time.time - progressAt > GiveUpSeconds)
            {
                skipUntil[prey] = Time.time + SkipSeconds;
                prey = null;
                scanAt = 0f;
                gaveUp = true;
            }
            return prey;
        }

        private CharacterBody Nearest(TeamIndex own, Vector3 pos)
        {
            CharacterBody result = null;
            float bestSqr = float.MaxValue;
            float now = Time.time;
            for (int t = 0; t < HostileTeams.Length; t++)
            {
                var members = TeamComponent.GetTeamMembers(HostileTeams[t]);
                for (int i = 0; i < members.Count; i++)
                {
                    CharacterBody b = members[i] ? members[i].body : null;
                    if (!b || (skipUntil.TryGetValue(b, out float until) && until > now)) continue;
                    if (!Perception.IsTargetable(b, own)) continue;
                    Vector3 d = b.footPosition - pos;
                    d.y *= HeightWeight;
                    float sqr = d.sqrMagnitude;
                    if (sqr < bestSqr) { bestSqr = sqr; result = b; }
                }
            }
            return result;
        }
    }
}
