using System;
using System.Collections.Generic;
using HighCommand.Client;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace HighCommand.UI
{
    /// <summary>
    /// The game's own art for order marks, loaded once from its Addressables: the ping's icons and chevron, the shockwave
    /// and glow masks of its effects. A picture that fails to load falls back to one of the mode's drawn sprites.
    /// </summary>
    internal static class GameArt
    {
        private static Sprite chevron, sprint, attack, chest, shockwave, glow;

        /// <summary>The ping's chevron (points down, a dark rim baked in).</summary>
        internal static Sprite Chevron => Get(ref chevron, "RoR2/Base/UI/texChevronPlayer.png", UiKit.Marker);
        /// <summary>The running man of a plain ping.</summary>
        internal static Sprite Sprint => Get(ref sprint, "RoR2/Base/Common/MiscIcons/texSprintIcon.png", UiKit.GlyphSprite(Glyph.Move));
        /// <summary>The crossed swords of an enemy ping.</summary>
        internal static Sprite Attack => Get(ref attack, "RoR2/Base/Common/MiscIcons/texAttackIcon.png", UiKit.GlyphSprite(Glyph.Attack));
        /// <summary>The chest of an interactable ping.</summary>
        internal static Sprite Chest => Get(ref chest, "RoR2/Base/Common/MiscIcons/texInventoryIconOutlined.png", UiKit.GlyphSprite(Glyph.Interact));
        /// <summary>A rough ring (the mask of the game's shockwaves).</summary>
        internal static Sprite Shockwave => Get(ref shockwave, "RoR2/Base/Common/VFX/ParticleMasks/texShockwaveRing1MaskCentered.png", UiKit.Ring);
        /// <summary>A soft round light (the mask of the game's flashes).</summary>
        internal static Sprite Glow => Get(ref glow, "RoR2/Base/Common/VFX/ParticleMasks/texParticleDust2Mask.png", UiKit.Glow);

        /// <summary>Loads everything now (with the stage), so the first click does not wait for the loading.</summary>
        internal static void Preload()
        {
            Sprite _ = Chevron; _ = Sprint; _ = Attack; _ = Chest; _ = Shockwave; _ = Glow;
        }

        private static Sprite Get(ref Sprite slot, string key, Sprite fallback)
        {
            if (slot) return slot;
            Texture2D tex = null;
            try { tex = Addressables.LoadAssetAsync<Texture2D>(key).WaitForCompletion(); }
            catch (Exception e) { Plugin.Log.LogWarning($"GameArt: could not load {key}: {e.Message}"); }
            if (tex)
            {
                slot = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                slot.hideFlags = HideFlags.HideAndDontSave;
            }
            else slot = fallback;
            return slot;
        }
    }

    /// <summary>
    /// Order marks in the look of the game's ping. The click itself: a shockwave and a flash on the ground for a moment.
    /// Then, while the order lasts: a thin beam from the place up to the order's picture over the ping's chevrons (over
    /// an enemy, a friend or an object: the picture and chevrons above it, no beam). The marks pop in like a ping, fade
    /// out when the order ends and are drawn on top of the world, as the game's pings are. The routes are not drawn
    /// here: they show only while Shift is held (<see cref="WorldOverlay"/>).
    /// </summary>
    internal sealed class OrderBeacons
    {
        /// <summary>The colours of the game's pings: a place, an enemy, an interactable.</summary>
        internal static readonly Color PingGreen = new Color(0.525f, 0.961f, 0.486f, 1f);
        internal static readonly Color PingRed = new Color(0.93f, 0.2f, 0.18f, 1f);
        internal static readonly Color PingYellow = new Color(0.886f, 0.871f, 0.173f, 1f);

        private sealed class Beacon
        {
            internal Orders.OrderKind Kind;
            internal Vector3 Point;
            internal GameObject Target;
            internal bool OnTarget;      // made for a target: gone with it (not left on the ground)
            internal bool Queued;
            internal Color Color;
            internal float Born;
            internal float Alpha;
            internal bool Seen;
            internal Sprite Icon;
            internal bool Outlined;      // the game's icons carry their own dark rim; the mode's glyphs get a shadow
            internal float Phase;        // the bobbing of the marks is not in step
        }

        private readonly RectTransform flashLayer, markLayer;
        private readonly List<Beacon> beacons = new List<Beacon>(16);
        private readonly Stack<Beacon> spare = new Stack<Beacon>(16);
        private readonly List<Image> flashImages = new List<Image>(16);
        private readonly List<Image> markImages = new List<Image>(64);
        private int flashUsed, marksUsed;
        private float scale = 1f;
        private Camera cam;

        private static Sprite beam;

        internal OrderBeacons(Transform parent)
        {
            flashLayer = UiKit.NewRect("OrderFlashes", parent);
            UiKit.Stretch(flashLayer);
            markLayer = UiKit.NewRect("OrderBeacons", parent);
            UiKit.Stretch(markLayer);
            GameArt.Preload();
        }

        internal int Count => beacons.Count;

        /// <summary>Starts a frame: every mark has to be asked for again or it fades out.</summary>
        internal void Begin(Camera camera, float layerScale)
        {
            cam = camera;
            scale = Mathf.Max(0.0001f, layerScale);
            marksUsed = 0;
            flashUsed = 0;
            for (int i = 0; i < beacons.Count; i++) beacons[i].Seen = false;
        }

        /// <summary>A mark for an order this frame: on the ground at <paramref name="point"/>, or over the target.</summary>
        internal void Want(Orders.OrderKind kind, Vector3 point, GameObject target, bool queued, Color color)
        {
            if (point == Vector3.zero && !target) return;
            Beacon b = null;
            for (int i = 0; i < beacons.Count && b == null; i++)
            {
                Beacon o = beacons[i];
                if (o.Kind != kind || o.Queued != queued || o.OnTarget != (bool)target) continue;
                if (!(target ? o.Target == target : (o.Point - point).sqrMagnitude < 5f * 5f)) continue;
                // two groups sent to one place (heroes standing apart): one mark is enough
                if (o.Seen) return;
                b = o;
            }
            if (b == null)
            {
                b = spare.Count > 0 ? spare.Pop() : new Beacon();
                b.Kind = kind;
                b.Target = target;
                b.OnTarget = target;
                b.Queued = queued;
                b.Born = Time.unscaledTime;
                b.Phase = UnityEngine.Random.value * 6.28f;
                b.Icon = IconOf(kind, target, out b.Outlined);
                beacons.Add(b);
            }
            b.Point = point;
            b.Color = color;
            b.Seen = true;
        }

        /// <summary>Draws the marks (call after every <see cref="Want"/> of the frame) and fades out the ones not asked for.</summary>
        internal void DrawMarks()
        {
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            for (int i = beacons.Count - 1; i >= 0; i--)
            {
                Beacon b = beacons[i];
                b.Alpha = b.Seen ? Mathf.Min(1f, b.Alpha + dt / 0.1f) : b.Alpha - dt / 0.3f;
                if (b.Alpha <= 0f || (b.OnTarget && !b.Target))
                {
                    beacons.RemoveAt(i);
                    b.Target = null;
                    b.Icon = null;
                    spare.Push(b);
                    continue;
                }
                if (cam) DrawBeacon(b, now);
            }
        }

        // ---- one mark ---------------------------------------------------------------------------

        private void DrawBeacon(Beacon b, float now)
        {
            float age = now - b.Born;
            float a = b.Alpha * (b.Queued ? 0.55f : 0.9f);
            float size = b.Queued ? 0.8f : 1f;
            float pop = Pop(age);
            float bob = Mathf.Sin(now * 3.2f + b.Phase) * 2.5f;
            Color c = b.Color;

            Vector2 top;
            if (b.OnTarget)
            {
                // over a body (above its health bar) or an object (above its price tag): no beam
                Vector3 anchor;
                float above;
                CharacterBody body = b.Target.GetComponent<CharacterBody>();
                if (body)
                {
                    anchor = body.corePosition + Vector3.up * (Mathf.Max(1.2f, body.radius * 1.8f) + 0.6f);
                    above = 16f;
                }
                else
                {
                    anchor = b.Target.transform.position + Vector3.up * 1.8f;
                    above = 20f;
                }
                if (!WorldPicker.ProjectPoint(cam, anchor, out Vector2 s) || !WorldPicker.IsOnScreen(s)) return;
                top = s + new Vector2(0f, (above + bob) * scale);
            }
            else
            {
                if (!WorldPicker.ProjectPoint(cam, b.Point, out Vector2 s) || !WorldPicker.IsOnScreen(s)) return;
                // the light on the ground, then the beam growing up out of it
                float rise = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / 0.18f), 2f);
                float height = 64f * size * rise;
                Img(GameArt.Glow, s, new Vector2(40f, 40f * Flatness()) * size, Fade(c, a * 0.75f));
                Img(Beam, s, new Vector2(14f * size, height), Fade(c, a * 0.3f), pivotY: 0f);
                Img(Beam, s, new Vector2(3f * size, height), Fade(Color.Lerp(c, Color.white, 0.45f), a * 0.9f), pivotY: 0f);
                top = s + new Vector2(0f, (height + bob) * scale);
            }

            // the ping's double chevron pointing down at the place, the order's picture above it
            float chev = 27f * size * pop;
            Img(GameArt.Chevron, top, new Vector2(chev, chev), Fade(c, a));
            Img(GameArt.Chevron, top + new Vector2(0f, 9f * size * scale), new Vector2(chev, chev), Fade(c, a * 0.75f));
            float icon = 38f * size * pop;
            Vector2 at = top + new Vector2(0f, (9f + 27f) * size * scale);
            if (!b.Outlined) Img(b.Icon, at + new Vector2(0f, -1f * scale), new Vector2(icon + 5f, icon + 5f), new Color(0f, 0f, 0f, 0.75f * a));
            Img(b.Icon, at, new Vector2(icon, icon), Fade(c, a));
        }

        // the game's ping grows past its size and settles back (its ObjectScaleCurve, 0.2 s)
        private static float Pop(float age)
        {
            if (age >= 0.24f) return 1f;
            if (age < 0.12f)
            {
                float t = age / 0.12f;
                return 1.25f * (1f - (1f - t) * (1f - t));
            }
            return Mathf.Lerp(1.25f, 1f, (age - 0.12f) / 0.12f);
        }

        private static Sprite IconOf(Orders.OrderKind kind, GameObject target, out bool outlined)
        {
            outlined = true;
            switch (kind)
            {
                case Orders.OrderKind.Move: return GameArt.Sprint;
                case Orders.OrderKind.Attack:
                case Orders.OrderKind.AttackMove: return GameArt.Attack;
                case Orders.OrderKind.Interact:
                    if (target)
                    {
                        try
                        {
                            Sprite s = PingIndicator.GetInteractableIcon(target);
                            if (s) return s;
                        }
                        catch (Exception) { /* the chest picture will do */ }
                    }
                    return GameArt.Chest;
            }
            outlined = false;
            switch (kind)
            {
                case Orders.OrderKind.Retreat: return UiKit.GlyphSprite(Glyph.Retreat);
                case Orders.OrderKind.Patrol: return UiKit.GlyphSprite(Glyph.Patrol);
                case Orders.OrderKind.Follow: return UiKit.GlyphSprite(Glyph.Follow);
                default: return UiKit.GlyphSprite(Glyph.Move);
            }
        }

        // ---- the click: a shockwave and a flash on the ground ------------------------------------

        /// <summary>Draws the confirmations of the last clicks (<see cref="Feedback.Pings"/>), removing the finished ones.</summary>
        internal void DrawFlashes()
        {
            float now = Time.unscaledTime;
            float flat = Flatness();
            var list = Feedback.Pings;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var p = list[i];
                float age = now - p.born;
                if (age > p.life) { list.RemoveAt(i); continue; }
                float t = age / p.life;

                // the ring runs out fast and slows down; the flash in the middle is gone in the first half. Sizes are
                // the world's, but never below a floor on the screen: a click far away is seen as well.
                float grow = 1f - Mathf.Pow(1f - t, 3f);
                float floor = Mathf.Min(p.size, 1.5f);
                if (WorldPicker.Project(p.position, p.size * (0.6f + 2.4f * grow), out Vector2 s, out float px) && WorldPicker.IsOnScreen(s, 200f))
                {
                    float w = Mathf.Max(px * 2f / scale, (50f + 120f * grow) * floor);
                    Color ring = Color.Lerp(p.color, Color.white, 0.5f * (1f - t));
                    float ra = Mathf.Pow(1f - t, 1.4f);
                    FlashImg(GameArt.Shockwave, s, new Vector2(w, w * flat), Fade(ring, ra));
                    // the second, a little smaller: the thin rough ring reads thicker
                    FlashImg(GameArt.Shockwave, s, new Vector2(w * 0.95f, w * 0.95f * flat), Fade(ring, ra * 0.7f));
                    // a slower inner ring
                    float wi = w * (0.45f + 0.25f * grow);
                    FlashImg(GameArt.Shockwave, s, new Vector2(wi, wi * flat), Fade(p.color, ra * 0.5f));
                }
                if (t < 0.5f && WorldPicker.Project(p.position, p.size * (1.2f + 0.8f * t), out Vector2 g, out float gpx) && WorldPicker.IsOnScreen(g, 100f))
                {
                    float w = Mathf.Max(gpx * 2f / scale, 70f * floor);
                    Color flash = Color.Lerp(p.color, Color.white, 0.6f);
                    FlashImg(GameArt.Glow, g, new Vector2(w, w * flat), Fade(flash, Mathf.Pow(1f - t / 0.5f, 2f) * 0.9f));
                }
            }
        }

        /// <summary>Hides the pooled pictures not used this frame.</summary>
        internal void End()
        {
            for (int i = marksUsed; i < markImages.Count; i++)
                if (markImages[i].enabled) markImages[i].enabled = false;
            for (int i = flashUsed; i < flashImages.Count; i++)
                if (flashImages[i].enabled) flashImages[i].enabled = false;
        }

        internal void Clear()
        {
            for (int i = 0; i < beacons.Count; i++) { beacons[i].Target = null; spare.Push(beacons[i]); }
            beacons.Clear();
        }

        // ---- helpers ------------------------------------------------------------------------------

        // marks lying on the ground are flattened by the camera's tilt
        private static float Flatness()
        {
            RtsCamera camera = RtsCamera.Instance;
            float pitch = camera ? camera.Pitch : Settings.CameraPitch.Value;
            return Mathf.Clamp(Mathf.Sin(pitch * Mathf.Deg2Rad), 0.35f, 1f);
        }

        private static Color Fade(Color c, float alpha)
        {
            c.a = Mathf.Clamp01(alpha);
            return c;
        }

        private void Img(Sprite sprite, Vector2 screen, Vector2 size, Color color, float pivotY = 0.5f)
        {
            if (marksUsed >= markImages.Count) markImages.Add(UiKit.NewImage("Mark", markLayer, sprite, color));
            Place(markImages[marksUsed++], sprite, screen, size, color, pivotY);
        }

        private void FlashImg(Sprite sprite, Vector2 screen, Vector2 size, Color color)
        {
            if (flashUsed >= flashImages.Count) flashImages.Add(UiKit.NewImage("Flash", flashLayer, sprite, color));
            Place(flashImages[flashUsed++], sprite, screen, size, color, 0.5f);
        }

        private static void Place(Image img, Sprite sprite, Vector2 screen, Vector2 size, Color color, float pivotY)
        {
            if (!img.enabled) img.enabled = true;
            if (img.sprite != sprite) img.sprite = sprite;
            img.color = color;
            RectTransform rt = img.rectTransform;
            if (rt.pivot.y != pivotY) rt.pivot = new Vector2(0.5f, pivotY);
            rt.sizeDelta = size;
            rt.position = new Vector3(screen.x, screen.y, 0f);
        }

        /// <summary>A thin vertical light: bright at the bottom, gone at the top, soft at the sides.</summary>
        private static Sprite Beam
        {
            get
            {
                if (beam) return beam;
                const int w = 16, h = 64;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var pixels = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float across = Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                        float side = Mathf.Clamp01((1f - across) / 0.6f);
                        float up = (y + 0.5f) / h;
                        float a = side * side * Mathf.Pow(1f - up, 1.3f) * Mathf.Clamp01(up * 12f + 0.4f);
                        pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                    }
                tex.SetPixels32(pixels);
                tex.Apply(false, true);
                beam = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
                beam.hideFlags = HideFlags.HideAndDontSave;
                return beam;
            }
        }
    }
}
