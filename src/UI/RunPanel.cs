using HighCommand.Army;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace HighCommand.UI
{
    /// <summary>
    /// Top right: the game mode's own panel, exactly as the stock HUD hosts it (Run.InstantiateUi): the run timer, the
    /// difficulty bar, the stage and level, and the objective panel. The objectives are the army's (<see cref="ArmyObjectives"/>);
    /// a click on them does what the teleporter key does.
    /// </summary>
    internal sealed class RunPanel
    {
        // the stock UpperRightCluster: the panel lays itself out round a rect of this size in the corner
        private static readonly Vector2 ClusterSize = new Vector2(128f, 51f);

        private readonly RectTransform anchor;
        private readonly RectTransform canvasRect;
        private GameObject instance;
        private Run hostRun;
        private ObjectivePanelController objectives;
        private RectTransform objectiveRect;

        internal RunPanel(Transform parent)
        {
            canvasRect = parent as RectTransform;
            anchor = UiKit.NewRect("RunPanel", parent);
            UiKit.Place(anchor, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Theme.Margin * 0.5f, -Theme.Margin * 0.5f), ClusterSize);
            ArmyObjectives.Hook();
        }

        internal void SetVisible(bool visible)
        {
            if (anchor.gameObject.activeSelf != visible) anchor.gameObject.SetActive(visible);
        }

        /// <summary>The objective panel (a click there acts on the teleporter).</summary>
        internal bool ObjectivesContain(Vector2 screenPoint) =>
            objectiveRect && objectiveRect.gameObject.activeInHierarchy && UiKit.Contains(objectiveRect, screenPoint);

        /// <summary>Canvas units below the top edge where the objective panel ends (the loot feed goes under it).</summary>
        internal float Bottom
        {
            get
            {
                if (!objectiveRect || !objectiveRect.gameObject.activeInHierarchy || !canvasRect) return 140f;
                Vector3[] corners = Corners;
                objectiveRect.GetWorldCorners(corners);
                Vector3 local = canvasRect.InverseTransformPoint(corners[0]);
                return canvasRect.rect.yMax - local.y;
            }
        }

        private static readonly Vector3[] Corners = new Vector3[4];

        internal void Tick()
        {
            if (!anchor.gameObject.activeInHierarchy) return;
            Run run = Run.instance;
            if (run != hostRun) Rebuild(run);
            if (objectives)
            {
                // the commander's master: the stock trackers ask a master; ours count the heroes themselves
                CharacterMaster master = LootService.PoolMaster;
                if (master) objectives.SetCurrentMaster(master);
            }
        }

        private void Rebuild(Run run)
        {
            Release();
            hostRun = run;
            if (!run) return;
            try
            {
                instance = run.InstantiateUi(anchor);
                if (!instance) return;
                objectives = instance.GetComponentInChildren<ObjectivePanelController>(true);
                objectiveRect = objectives ? (RectTransform)objectives.transform : null;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError("RunPanel: the run's own panel could not be shown: " + e);
                Release();
            }
        }

        private void Release()
        {
            if (hostRun && instance) hostRun.UnregisterUi(instance);
            if (instance) Object.Destroy(instance);
            instance = null;
            objectives = null;
            objectiveRect = null;
            hostRun = null;
        }

        internal void Destroy()
        {
            ArmyObjectives.Unhook();
            Release();
            if (anchor) Object.Destroy(anchor.gameObject);
        }
    }
}
