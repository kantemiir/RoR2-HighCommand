using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HighCommand.UI
{
    /// <summary>
    /// The game's own interface art, loaded once through Addressables: the frames, plates, outlines and highlights of the
    /// stock HUD and menus, the cooldown material, a few of its icons. Each lookup has a drawn stand-in of the same shape,
    /// so a renamed asset in a future game version makes the interface plainer, never broken.
    /// </summary>
    internal static class HudArt
    {
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Texture> Textures = new Dictionary<string, Texture>();
        private static readonly HashSet<string> Missing = new HashSet<string>();

        // ---- frames and plates (9-sliced) ---------------------------------------------------------------

        /// <summary>The thin frame round a HUD cluster (money, the bars of the stock HUD).</summary>
        internal static Sprite ClusterFrame => Load("RoR2/Base/UI/texUIHighlightHeader.png", new Vector4(18, 18, 18, 18), () => Frame(64, 3, 18));

        /// <summary>A solid plate: under key labels, timers, the skill bar.</summary>
        internal static Sprite Plate => Load("RoR2/Base/UI/texUIMainHealthbar.png", new Vector4(21, 20, 19, 19), () => Solid(32, 8));

        /// <summary>A plate with its top right corner cut off (the run timer, the objective panel).</summary>
        internal static Sprite PlateCut => Load("RoR2/Base/UI/texUICutOffCorner.png", new Vector4(24, 24, 24, 24), () => UiKit.Shape(10f));

        /// <summary>The plate behind a skill icon.</summary>
        internal static Sprite SkillPlate => Load("RoR2/Base/UI/texUICombatHealthbar.png", new Vector4(19, 18, 18, 18), () => Solid(32, 8));

        /// <summary>A hairline outline.</summary>
        internal static Sprite Outline => Load("RoR2/Base/UI/texUIOutlineOnly.png", new Vector4(4, 4, 4, 4), () => Frame(16, 1, 4));

        /// <summary>The thin white frame of a ready skill / an avatar.</summary>
        internal static Sprite HighlightThin => Load("RoR2/Base/UI/texUIHighlightBoxOutline.png", new Vector4(4, 4, 4, 4), () => Frame(16, 2, 4));

        /// <summary>The thick white frame that appears under the pointer (buttons, strips).</summary>
        internal static Sprite HighlightThick => Load("RoR2/Base/UI/texUIHighlightBoxOutlineThick.png", new Vector4(6, 12, 12, 6), () => Frame(32, 3, 8));

        /// <summary>The thick hover frame of square icons (survivor cards, picker buttons).</summary>
        internal static Sprite HighlightIcon => Load("RoR2/Base/UI/texUIHighlightBoxOutlineThickIcon.png", new Vector4(6, 6, 6, 6), () => Frame(32, 3, 8));

        /// <summary>The face of every menu button and card.</summary>
        internal static Sprite CleanButton => Load("RoR2/Base/UI/texUICleanButton.png", new Vector4(8, 8, 8, 8), () => Solid(32, 8));

        /// <summary>The face of a dialog's header and inner panels.</summary>
        internal static Sprite CleanPanel => Load("RoR2/Base/UI/texUICleanPanel.png", new Vector4(8, 8, 8, 8), () => Solid(32, 8));

        /// <summary>A strip that fades out at both ends (the stage name, notifications, the experience bar).</summary>
        internal static Sprite BackdropFaded => Load("RoR2/Base/UI/texUIBackdropFadedEnds.png", new Vector4(32, 2, 32, 2), FadedStrip);

        /// <summary>Two hairlines, above and below (a legend strip, the focus of a scoreboard row).</summary>
        internal static Sprite HeaderDouble => Load("RoR2/Base/UI/texUIHeaderDouble.png", new Vector4(9, 9, 9, 9), () => Frame(32, 1, 9));

        /// <summary>The soft square flash of a skill that is ready again.</summary>
        internal static Sprite FlashSquare => Load("RoR2/Base/UI/texUIFlashSquare1.png", Vector4.zero, () => UiKit.Glow);

        // ---- checkboxes (an atlas of two) ------------------------------------------------------------------

        internal static Sprite CheckboxOff => Cut("RoR2/Base/UI/texUICheckboxes.png", 0, 2, () => Frame(16, 2, 4));

        internal static Sprite CheckboxOn => Cut("RoR2/Base/UI/texUICheckboxes.png", 1, 2, () => Solid(16, 4));

        // ---- icons -----------------------------------------------------------------------------------------

        internal static Sprite Coins => Load("RoR2/Base/UI/texCoinsIcon.png", Vector4.zero, () => Glyph(UI.Glyph.Coin));
        internal static Sprite Inventory => Load("RoR2/Base/Common/MiscIcons/texInventoryIcon.png", Vector4.zero, () => Glyph(UI.Glyph.Chest));
        internal static Sprite Attack => Load("RoR2/Base/Common/MiscIcons/texAttackIcon.png", Vector4.zero, () => Glyph(UI.Glyph.AttackMove));
        internal static Sprite Sprint => Load("RoR2/Base/Common/MiscIcons/texSprintIcon.png", Vector4.zero, () => Glyph(UI.Glyph.Move));
        internal static Sprite Teleporter => Load("RoR2/Base/Common/MiscIcons/texTeleporterIcon.png", Vector4.zero, () => Glyph(UI.Glyph.Teleporter));
        internal static Sprite Loot => Load("RoR2/Base/Common/MiscIcons/texLootIconOutlined.png", Vector4.zero, () => Glyph(UI.Glyph.SeekChests));
        internal static Sprite DeadChevron => Load("RoR2/Base/UI/texChevronDead.png", Vector4.zero, () => Glyph(UI.Glyph.Cancel));

        /// <summary>The bubbly backdrop of a survivor card in the character select.</summary>
        internal static Texture SurvivorBackground => LoadTexture("RoR2/Base/Common/texSurvivorBGIcon.png");

        /// <summary>The ramp under the money in the stock HUD (white, fading to the right).</summary>
        internal static Texture MoneyRamp => LoadTexture("RoR2/Base/Common/ColorRamps/texRampWhiteAlphaOnly.png");

        // ---- the cooldown of a skill (the stock remap shader; a plain dark sweep without it) ------------------

        private static Material cooldownMaterial;
        private static bool cooldownSearched;

        /// <summary>The material of the stock skill cooldown: the remap texture is revealed by the image's alpha.</summary>
        internal static Material CooldownMaterial
        {
            get
            {
                if (cooldownSearched) return cooldownMaterial;
                cooldownSearched = true;
                const string key = "RoR2/Base/UI/matUISkillCD.mat";
                if (!Exists<Material>(key)) { Note(key, null); return null; }
                try { cooldownMaterial = Addressables.LoadAssetAsync<Material>(key).WaitForCompletion(); }
                catch (Exception e) { Note(key, e); }
                return cooldownMaterial;
            }
        }

        internal static Texture CooldownRemap => LoadTexture("RoR2/Base/UI/texCooldownRemap.png");

        // ---- the commander's abilities: the game's own pictures for the same ideas -----------------------------

        private static readonly Sprite[] abilityIcons = new Sprite[4];

        /// <summary>
        /// Orbital strike: Captain's orbital probe; field medic: the medkit; war cry: the warbanner (it is the same buff);
        /// mark: the death mark (the same idea: the marked one takes more damage). A drawn glyph when one is missing.
        /// </summary>
        internal static Sprite AbilityIcon(int index)
        {
            if (index < 0 || index >= abilityIcons.Length) return UiKit.White;
            if (abilityIcons[index]) return abilityIcons[index];
            Sprite s = null;
            switch (index)
            {
                case 0:
                    const string airstrike = "RoR2/Base/Captain/CallAirstrike.asset";
                    if (!Exists<RoR2.Skills.SkillDef>(airstrike)) { Note(airstrike, null); break; }
                    try
                    {
                        var skill = Addressables.LoadAssetAsync<RoR2.Skills.SkillDef>(airstrike).WaitForCompletion();
                        if (skill) s = skill.icon;
                    }
                    catch (Exception e) { Note(airstrike, e); }
                    break;
                case 1: s = Load("RoR2/Base/Medkit/texMedkitIcon.png", Vector4.zero, null); break;
                case 2: s = Load("RoR2/Base/WardOnLevel/texWarbannerIcon.png", Vector4.zero, null); break;
                case 3: s = Load("RoR2/Base/DeathMark/texDeathMarkIcon.png", Vector4.zero, null); break;
            }
            return abilityIcons[index] = s ? s : Glyph(CommanderLook.GlyphOf(index));
        }

        // ---- the mode's own pictures in the stock icon style ---------------------------------------------------

        private const int GlyphSize = 128;
        private const int GlyphInner = 112;
        private const int GlyphShadow = 5;
        private static readonly Sprite[] glyphs = new Sprite[(int)UI.Glyph.Count];

        /// <summary>
        /// A glyph as the game draws its flat icons (texSprintIcon, texInventoryIcon): a white silhouette with a hard dark
        /// copy down and to the right. White, so the image colour tints it; the shadow stays dark under any tint.
        /// </summary>
        internal static Sprite Glyph(Glyph glyph)
        {
            int index = (int)glyph;
            if (index < 0 || index >= glyphs.Length) return UiKit.White;
            if (glyphs[index]) return glyphs[index];

            float[] art = GlyphArt.Render(glyph, GlyphInner);
            const int ox = (GlyphSize - GlyphInner) / 2 - GlyphShadow / 2, oy = (GlyphSize - GlyphInner) / 2 + GlyphShadow / 2;
            var pixels = new Color32[GlyphSize * GlyphSize];
            for (int y = 0; y < GlyphSize; y++)
                for (int x = 0; x < GlyphSize; x++)
                {
                    float a = Sample(art, x - ox, y - oy);
                    float s = Sample(art, x - ox - GlyphShadow, y - oy + GlyphShadow) * 0.9f;
                    float alpha = a + s * (1f - a);
                    float light = alpha > 0f ? a / alpha : 1f; // white over a near-black shadow
                    byte l = (byte)Mathf.RoundToInt(Mathf.Lerp(14f, 255f, light));
                    pixels[y * GlyphSize + x] = new Color32(l, l, l, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            var tex = new Texture2D(GlyphSize, GlyphSize, TextureFormat.RGBA32, true)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear
            };
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, GlyphSize, GlyphSize), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return glyphs[index] = sprite;
        }

        private static float Sample(float[] art, int x, int y)
        {
            if (x < 0 || y < 0 || x >= GlyphInner || y >= GlyphInner) return 0f;
            return Mathf.Clamp01(art[y * GlyphInner + x]);
        }

        /// <summary>Loads everything the HUD shows at once (with the stage), so the first frame does not stall.</summary>
        internal static void Preload()
        {
            Sprite _ = ClusterFrame; _ = Plate; _ = PlateCut; _ = SkillPlate; _ = Outline; _ = HighlightThin; _ = HighlightThick;
            _ = HighlightIcon; _ = CleanButton; _ = CleanPanel; _ = BackdropFaded; _ = HeaderDouble; _ = FlashSquare;
            _ = CheckboxOff; _ = CheckboxOn; _ = Coins; _ = Inventory; _ = Teleporter;
            Material __ = CooldownMaterial;
            Texture ___ = CooldownRemap;
            for (int i = 0; i < abilityIcons.Length; i++) _ = AbilityIcon(i);
        }

        // ---- loading ---------------------------------------------------------------------------------------

        private static Sprite Load(string key, Vector4 border, Func<Sprite> fallback)
        {
            if (Sprites.TryGetValue(key, out Sprite cached) && cached) return cached;
            Sprite sprite = null;
            if (!Missing.Contains(key))
            {
                // the asset's own sprite keeps its slicing; a plain texture is sliced here as the game's prefabs slice it
                if (Exists<Sprite>(key))
                {
                    try { sprite = Addressables.LoadAssetAsync<Sprite>(key).WaitForCompletion(); }
                    catch (Exception e) { Note(key, e); }
                }
                if (!sprite && LoadTexture(key) is Texture2D tex)
                {
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                        SpriteMeshType.FullRect, border);
                    sprite.hideFlags = HideFlags.HideAndDontSave;
                }
                if (!sprite) Missing.Add(key);
            }
            if (!sprite && fallback != null) sprite = fallback();
            if (sprite) Sprites[key] = sprite;
            return sprite;
        }

        private static Texture LoadTexture(string key)
        {
            if (Textures.TryGetValue(key, out Texture cached) && cached) return cached;
            if (Missing.Contains(key + "#tex")) return null;
            Texture tex = null;
            if (Exists<Texture2D>(key))
            {
                try { tex = Addressables.LoadAssetAsync<Texture2D>(key).WaitForCompletion(); }
                catch (Exception e) { Note(key, e); }
            }
            else Note(key, null);
            if (tex) Textures[key] = tex;
            else Missing.Add(key + "#tex");
            return tex;
        }

        // A load of a key that is not there makes Addressables log an error of its own: look the key up first.
        private static bool Exists<T>(string key)
        {
            try
            {
                var handle = Addressables.LoadResourceLocationsAsync(key, typeof(T));
                var locations = handle.WaitForCompletion();
                bool found = locations != null && locations.Count > 0;
                Addressables.Release(handle);
                return found;
            }
            catch (Exception) { return false; }
        }

        /// <summary>One cell of a texture split into <paramref name="cells"/> equal columns.</summary>
        private static Sprite Cut(string key, int cell, int cells, Func<Sprite> fallback)
        {
            string id = key + "#" + cell;
            if (Sprites.TryGetValue(id, out Sprite cached) && cached) return cached;
            Sprite sprite = null;
            if (LoadTexture(key) is Texture2D tex)
            {
                float w = tex.width / (float)cells;
                sprite = Sprite.Create(tex, new Rect(w * cell, 0, w, tex.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.hideFlags = HideFlags.HideAndDontSave;
            }
            if (!sprite) sprite = fallback();
            Sprites[id] = sprite;
            return sprite;
        }

        private static void Note(string key, Exception e) =>
            Plugin.Log.LogWarning($"HudArt: {key} is not available ({(e != null ? e.GetType().Name : "no such key")}); a drawn stand-in is used.");

        // ---- drawn stand-ins -----------------------------------------------------------------------------------

        private static Sprite Solid(int size, int border) => Make(size, border, (x, y) => 1f);

        private static Sprite Frame(int size, int edge, int border) =>
            Make(size, border, (x, y) => Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y)) < edge ? 1f : 0f);

        private static Sprite FadedStrip()
        {
            const int w = 64, h = 4;
            return Make(w, h, 16, 1, (x, y) => Mathf.Clamp01(Mathf.Min(x, w - 1 - x) / 16f));
        }

        private static Sprite Make(int size, int border, Func<int, int, float> alpha) => Make(size, size, border, border, alpha);

        private static Sprite Make(int w, int h, int borderX, int borderY, Func<int, int, float> alpha)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(x, y)) * 255f));
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            Sprite s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(borderX, borderY, borderX, borderY));
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }
    }
}
