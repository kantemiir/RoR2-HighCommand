using RoR2;
using UnityEngine;
using UnityEngine.UI;

namespace HighCommand.UI
{
    /// <summary>
    /// The mode's interface on three canvases, drawn in this order:
    /// <list type="bullet">
    /// <item>World: what hangs on the world (bars, rings, marks, routes, the selection box). Its code places things in screen
    /// pixels as on an overlay canvas; an orthographic camera that maps one unit to one pixel draws it, so that the panels
    /// can be drawn over it (an overlay canvas always comes last).</item>
    /// <item>Hud: the panels, through a perspective camera set up as the game's UI camera, so that the corner clusters lean
    /// in round their corner as the stock ones do (left -6°, right +6°).</item>
    /// <item>Top: windows, the item card, messages and tooltips, an overlay canvas over everything.</item>
    /// </list>
    /// </summary>
    internal sealed class HudLayers
    {
        // after the scene and the game's own UI camera (depth 2)
        private const float WorldDepth = 50f, HudDepth = 51f;
        private const float WorldPlane = 10f;          // the world canvas lies on z = 0, the camera this far in front
        private const float ClipMargin = 1f;
        // the HUD camera stands away from everything else that is on the UI layer
        private static readonly Vector3 HudCameraAt = new Vector3(-5000f, 0f, 0f);

        internal readonly Canvas World;
        internal readonly Canvas Hud;
        internal readonly Canvas Top;
        internal readonly RectTransform UpperLeft, LowerLeft, UpperRight, LowerRight;

        private readonly Camera worldCamera, hudCamera;
        private readonly Canvas[] canvases;
        private readonly CanvasGroup[] groups;
        private int shownWidth = -1, shownHeight = -1;

        internal HudLayers(Transform owner, int topSortingOrder)
        {
            int layer = LayerIndex.ui.intVal;

            worldCamera = NewCamera("HighCommand.WorldCamera", WorldDepth, layer);
            worldCamera.orthographic = true;
            worldCamera.nearClipPlane = WorldPlane - ClipMargin;
            worldCamera.farClipPlane = WorldPlane + ClipMargin;
            World = NewCameraCanvas("HighCommand.World", owner, worldCamera, WorldPlane, layer);

            hudCamera = NewCamera("HighCommand.HudCamera", HudDepth, layer);
            hudCamera.transform.position = HudCameraAt;
            hudCamera.fieldOfView = Theme.HudCameraFov;
            hudCamera.nearClipPlane = 0.1f;
            hudCamera.farClipPlane = Theme.HudPlaneDistance * 4f;
            Hud = NewCameraCanvas("HighCommand.HUD", owner, hudCamera, Theme.HudPlaneDistance, layer);
            UiKit.UsePerspectiveCanvas(Hud, hudCamera);

            Top = UiKit.CreateCanvas("HighCommand.UI", topSortingOrder);
            Top.transform.SetParent(owner, false);

            UpperLeft = Cluster("UpperLeftCluster", new Vector2(0f, 1f), -Theme.ClusterTilt);
            LowerLeft = Cluster("BottomLeftCluster", new Vector2(0f, 0f), -Theme.ClusterTilt);
            UpperRight = Cluster("UpperRightCluster", new Vector2(1f, 1f), Theme.ClusterTilt);
            LowerRight = Cluster("BottomRightCluster", new Vector2(1f, 0f), Theme.ClusterTilt);

            canvases = new[] { World, Hud, Top };
            groups = new CanvasGroup[canvases.Length];
            for (int i = 0; i < canvases.Length; i++) groups[i] = canvases[i].gameObject.AddComponent<CanvasGroup>();
            Tick();
        }

        // A camera of its own at the top of the scene: whatever moves the owner never turns it.
        private static Camera NewCamera(string name, float depth, int layer)
        {
            var go = new GameObject(name);
            Object.DontDestroyOnLoad(go);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Depth;   // over the frame the game has drawn
            cam.cullingMask = 1 << layer;
            cam.depth = depth;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.useOcclusionCulling = false;
            return cam;
        }

        private static Canvas NewCameraCanvas(string name, Transform owner, Camera camera, float plane, int layer)
        {
            Canvas canvas = UiKit.CreateCanvas(name, 0);
            canvas.gameObject.layer = layer;
            canvas.transform.SetParent(owner, false);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = plane;
            return canvas;
        }

        // A cluster covers the whole canvas and turns round the vertical line through its corner: what hangs on that
        // corner keeps its place and leans in by the same angle as the stock cluster there.
        private RectTransform Cluster(string name, Vector2 corner, float tilt)
        {
            RectTransform rt = UiKit.NewRect(name, Hud.transform);
            UiKit.Stretch(rt);
            rt.pivot = corner;
            rt.localRotation = Quaternion.Euler(0f, tilt, 0f);
            return rt;
        }

        /// <summary>Keeps the world camera's one unit to one pixel when the window changes size.</summary>
        internal void Tick()
        {
            if (Screen.width == shownWidth && Screen.height == shownHeight) return;
            shownWidth = Screen.width;
            shownHeight = Screen.height;
            worldCamera.orthographicSize = shownHeight * 0.5f;
            worldCamera.transform.position = new Vector3(shownWidth * 0.5f, shownHeight * 0.5f, -WorldPlane);
        }

        /// <summary>The reference area of every canvas (the interface scale).</summary>
        internal void SetReferenceResolution(Vector2 reference)
        {
            for (int i = 0; i < canvases.Length; i++)
            {
                var scaler = canvases[i] ? canvases[i].GetComponent<CanvasScaler>() : null;
                if (scaler) scaler.referenceResolution = reference;
            }
        }

        internal void SetAlpha(float alpha)
        {
            for (int i = 0; i < groups.Length; i++)
                if (groups[i] && !Mathf.Approximately(groups[i].alpha, alpha)) groups[i].alpha = alpha;
        }

        internal void SetHidden(bool hidden)
        {
            for (int i = 0; i < canvases.Length; i++)
                if (canvases[i]) canvases[i].gameObject.SetActive(!hidden);
        }

        internal void Destroy()
        {
            UiKit.UsePerspectiveCanvas(null, null);
            for (int i = 0; i < canvases.Length; i++)
                if (canvases[i]) Object.Destroy(canvases[i].gameObject);
            if (worldCamera) Object.Destroy(worldCamera.gameObject);
            if (hudCamera) Object.Destroy(hudCamera.gameObject);
        }
    }
}
