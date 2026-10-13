using RoR2;
using RoR2.CharacterAI;
using RoR2.Navigation;
using UnityEngine;

namespace HighCommand.Orders
{
    /// <summary>
    /// Gets a walking hero unstuck (server; one per body executor). A hero that tries to walk but gets nowhere for
    /// <see cref="AiTuning.StuckSeconds"/> first jumps and asks for a new path; a little later it sidesteps, left and
    /// right in turn, jumping; as the last resort (<see cref="AiTuning.UnstuckTeleport"/>) it hops to walkable ground
    /// further along its way - the next point of its path when there is one, so it never lands on another level.
    /// </summary>
    internal sealed class StuckRecovery
    {
        private const float ProgressMeters = 1.2f;
        private const float SidestepAfter = 2f;      // seconds of being stuck before the sidesteps start
        private const float SidestepSeconds = 0.8f;
        private const float TeleportReach = 14f;
        private const float MinTeleportGap = 6f;
        private const float NextPointMin = 3f;       // a path point closer than this is not worth a hop
        private const float NodeSearch = 20f;

        private Vector3 progressPos;
        private float progressAt;
        private float stuckSince = -1f;
        private float sidestepUntil;
        private float sideSign = 1f;
        private float lastTeleport = -100f;

        internal bool Stuck => stuckSince >= 0f;

        /// <summary>Set for one update when the hero should jump.</summary>
        internal bool WantsJump { get; private set; }

        internal void Reset(Vector3 foot)
        {
            progressPos = foot;
            progressAt = Time.time;
            stuckSince = -1f;
            WantsJump = false;
        }

        /// <summary>
        /// Called every update while the hero walks to <paramref name="goal"/>. Returns a sidestep direction to walk
        /// instead of the path while one is due, null otherwise.
        /// </summary>
        internal Vector3? Update(Vector3 foot, Vector3 goal, bool tryingToMove, BaseAI ai, CharacterBody body, string who)
        {
            WantsJump = false;
            float now = Time.time;
            if (!tryingToMove || (foot - progressPos).sqrMagnitude > ProgressMeters * ProgressMeters)
            {
                Reset(foot);
                return null;
            }
            if (now - progressAt < AiTuning.StuckSeconds.Value) return null;

            if (stuckSince < 0f)
            {
                stuckSince = now;
                WantsJump = true;
                ai.broadNavigationAgent.InvalidatePath();
                if (Settings.VerboseLog.Value) Plugin.Log.LogInfo($"{who}: stuck at {foot:0}, jumping and looking for another way");
                return null;
            }

            float stuckFor = now - stuckSince;
            if (stuckFor > AiTuning.TeleportAfterSeconds.Value && AiTuning.UnstuckTeleport.Value && now - lastTeleport > MinTeleportGap)
            {
                Hop(foot, goal, ai, body, who);
                lastTeleport = now;
                Reset(foot);
                return null;
            }
            if (stuckFor < SidestepAfter) return null;

            if (now > sidestepUntil)
            {
                sideSign = -sideSign;
                sidestepUntil = now + SidestepSeconds;
                WantsJump = true;
                ai.broadNavigationAgent.InvalidatePath();
            }
            Vector3 toGoal = goal - foot;
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude < 0.01f) toGoal = body.transform.forward;
            return Vector3.Cross(Vector3.up, toGoal.normalized) * sideSign;
        }

        private static void Hop(Vector3 foot, Vector3 goal, BaseAI ai, CharacterBody body, string who)
        {
            Vector3? next = ai.broadNavigationAgent.output.nextPosition;
            Vector3 aim = next.HasValue && (next.Value - foot).sqrMagnitude > NextPointMin * NextPointMin
                ? next.Value
                : foot + Vector3.ClampMagnitude(goal - foot, TeleportReach);

            Vector3 landing = aim;
            SceneInfo info = SceneInfo.instance;
            NodeGraph graph = info ? info.groundNodes : null;
            NodeGraph.NodeIndex node = graph ? graph.FindClosestNode(aim, body.hullClassification, NodeSearch) : NodeGraph.NodeIndex.invalid;
            // the block-grid search can hand back a node much farther than asked: such a node is no good
            if (graph && node != NodeGraph.NodeIndex.invalid && graph.GetNodePosition(node, out Vector3 np)
                && (np - aim).sqrMagnitude <= NodeSearch * NodeSearch) landing = np;
            else if (Run.instance) landing = Run.instance.FindSafeTeleportPositionSimplified(body.hullClassification, aim);

            TeleportHelper.TeleportBody(body, landing, false);
            ai.broadNavigationAgent.InvalidatePath();
            Plugin.Log.LogInfo($"{who}: stuck for long at {foot:0}, hopped to {landing:0} on the way to {goal:0}");
        }
    }
}
