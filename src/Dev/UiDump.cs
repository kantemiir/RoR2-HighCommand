using System.Collections.Generic;
using System.Reflection;
using System.Text;
using RoR2;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace HighCommand.Dev
{
    /// <summary>
    /// Reading the game's own interface: prefab and live HUD hierarchies with their uGUI parts (rects, sprites, colours,
    /// fonts), UI skins and the TextMeshPro style sheet. The mode's interface is built to match what this prints.
    /// </summary>
    internal static class UiDump
    {
        private const ConVarFlags HostOnly = ConVarFlags.None;

        [ConCommand(commandName = "hc_dump_ui", flags = HostOnly, helpText = "hc_dump_ui <addressable prefab key | live:HUD | live:<object name>> [save] [depth]: prints a UI hierarchy with rects, images, texts; 'save' writes the sprites' textures to screenshots\\ui.")]
        private static void CcDumpUi(ConCommandArgs args)
        {
            if (args.Count < 1) throw new ConCommandException("Usage: hc_dump_ui <key | live:HUD | live:<name>> [save] [depth]");
            string key = args.GetArgString(0);
            bool save = args.Count > 1 && args.GetArgString(1) == "save";
            int depth = args.Count > 2 ? args.GetArgInt(2) : 40;
            var sb = new StringBuilder($"HighCommand ui '{key}':\n");
            var textures = new HashSet<Texture>();

            if (key.StartsWith("live:"))
            {
                string name = key.Substring(5);
                if (name == "HUD")
                {
                    foreach (RoR2.UI.HUD hud in RoR2.UI.HUD.readOnlyInstanceList)
                        if (hud) Walk(sb, hud.transform, 0, depth, textures);
                }
                else
                {
                    GameObject go = GameObject.Find(name);
                    if (go) Walk(sb, go.transform, 0, depth, textures);
                    else sb.AppendLine("  (no live object of that name)");
                }
            }
            else
            {
                GameObject go = null;
                try { go = Addressables.LoadAssetAsync<GameObject>(key).WaitForCompletion(); }
                catch (System.Exception e) { sb.AppendLine("  load failed: " + e.Message); }
                if (go) Walk(sb, go.transform, 0, depth, textures);
                else sb.AppendLine("  (not found)");
            }

            if (save) Save(sb, textures);
            Debug.Log(sb.ToString());
        }

        [ConCommand(commandName = "hc_dump_skin", flags = HostOnly, helpText = "hc_dump_skin <addressable key of a UISkinData>: prints its panel, text and button styles.")]
        private static void CcDumpSkin(ConCommandArgs args)
        {
            string key = args.GetArgString(0);
            var sb = new StringBuilder($"HighCommand skin '{key}':\n");
            RoR2.UI.UISkinData skin = null;
            try { skin = Addressables.LoadAssetAsync<RoR2.UI.UISkinData>(key).WaitForCompletion(); }
            catch (System.Exception e) { sb.AppendLine("  load failed: " + e.Message); }
            if (skin)
            {
                Panel(sb, "mainPanel", skin.mainPanelStyle);
                Panel(sb, "headerPanel", skin.headerPanelStyle);
                Text(sb, "headerText", skin.headerTextStyle);
                Panel(sb, "detailPanel", skin.detailPanelStyle);
                Text(sb, "detailText", skin.detailTextStyle);
                Text(sb, "bodyText", skin.bodyTextStyle);
                var b = skin.buttonStyle;
                sb.AppendLine($"  button sprite={Name(b.sprite)} material={Name(b.material)} normal={b.colors.normalColor} highlighted={b.colors.highlightedColor} pressed={b.colors.pressedColor} selected={b.colors.selectedColor} disabled={b.colors.disabledColor} mult={b.colors.colorMultiplier} fade={b.colors.fadeDuration} w={b.recommendedWidth} h={b.recommendedHeight}");
                Text(sb, "button.interactable", b.interactableTextStyle);
                Text(sb, "button.disabled", b.disabledTextStyle);
                Panel(sb, "scroll.background", skin.scrollRectStyle.backgroundPanelStyle);
            }
            Debug.Log(sb.ToString());
        }

        [ConCommand(commandName = "hc_dump_hbs", flags = HostOnly, helpText = "hc_dump_hbs <addressable key of a HealthBarStyle>: prints its bar colours and sprites.")]
        private static void CcDumpHealthBarStyle(ConCommandArgs args)
        {
            string key = args.GetArgString(0);
            var sb = new StringBuilder($"HighCommand health bar style '{key}':\n");
            RoR2.UI.HealthBarStyle style = null;
            try { style = Addressables.LoadAssetAsync<RoR2.UI.HealthBarStyle>(key).WaitForCompletion(); }
            catch (System.Exception e) { sb.AppendLine("  load failed: " + e.Message); }
            if (style)
            {
                sb.AppendLine($"  barPrefab={Name(style.barPrefab)} flashOnCritical={style.flashOnHealthCritical}");
                foreach (FieldInfo f in typeof(RoR2.UI.HealthBarStyle).GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (f.FieldType != typeof(RoR2.UI.HealthBarStyle.BarStyle)) continue;
                    var b = (RoR2.UI.HealthBarStyle.BarStyle)f.GetValue(style);
                    sb.AppendLine($"  {f.Name,-28} on={b.enabled} color={C(b.baseColor)} sprite={Name(b.sprite)} type={b.imageType} size={b.sizeDelta}");
                }
            }
            Debug.Log(sb.ToString());
        }

        [ConCommand(commandName = "hc_dump_styles", flags = HostOnly, helpText = "hc_dump_styles: prints the TextMeshPro style sheet the game's texts use (<style=cIsDamage> ...).")]
        private static void CcDumpStyles(ConCommandArgs args)
        {
            var sb = new StringBuilder("HighCommand TMP styles:\n");
            TMP_StyleSheet sheet = TMP_Settings.defaultStyleSheet;
            if (!sheet) sb.AppendLine("  (no default style sheet)");
            else
            {
                FieldInfo list = typeof(TMP_StyleSheet).GetField("m_StyleList", BindingFlags.Instance | BindingFlags.NonPublic);
                if (list?.GetValue(sheet) is List<TMP_Style> styles)
                    foreach (TMP_Style s in styles) sb.AppendLine($"  {s.name,-24} open='{s.styleOpeningDefinition}' close='{s.styleClosingDefinition}'");
            }
            sb.AppendLine("  fonts loaded: ");
            foreach (TMP_FontAsset f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                sb.AppendLine($"    {f.name} material={Name(f.material)}");
            Debug.Log(sb.ToString());
        }

        private static void Panel(StringBuilder sb, string what, RoR2.UI.UISkinData.PanelStyle p) =>
            sb.AppendLine($"  {what}: sprite={Name(p.sprite)} border={(p.sprite ? p.sprite.border.ToString() : "-")} material={Name(p.material)} color={p.color}");

        private static void Text(StringBuilder sb, string what, RoR2.UI.UISkinData.TextStyle t) =>
            sb.AppendLine($"  {what}: font={Name(t.font)} size={t.fontSize} align={t.alignment} color={t.color}");

        private static string Name(Object o) => o ? o.name : "null";

        private static void Walk(StringBuilder sb, Transform t, int level, int maxDepth, HashSet<Texture> textures)
        {
            string pad = new string(' ', level * 2);
            var rt = t as RectTransform;
            sb.Append($"{pad}{t.name} [{(t.gameObject.activeSelf ? "on" : "off")}]");
            if (rt)
                sb.Append($" anchor={V(rt.anchorMin)}-{V(rt.anchorMax)} pivot={V(rt.pivot)} pos={V(rt.anchoredPosition)} size={V(rt.sizeDelta)}");
            if (t.localScale != Vector3.one) sb.Append($" scale={t.localScale}");
            if (t.localEulerAngles != Vector3.zero) sb.Append($" rot={t.localEulerAngles}");
            sb.AppendLine();

            foreach (Component c in t.GetComponents<Component>())
            {
                if (!c || c is Transform || c is CanvasRenderer) continue;
                sb.Append($"{pad}  + {c.GetType().Name}");
                switch (c)
                {
                    case Image img:
                        sb.Append($" sprite={Name(img.sprite)} type={img.type} color={C(img.color)} mat={Name(img.material)} ppu={img.pixelsPerUnitMultiplier:0.##} raycast={img.raycastTarget}");
                        if (img.sprite)
                        {
                            sb.Append($" border={img.sprite.border} rect={img.sprite.rect.size}");
                            textures.Add(img.sprite.texture);
                        }
                        if (img.type == Image.Type.Filled) sb.Append($" fill={img.fillMethod}/{img.fillAmount:0.##}");
                        break;
                    case RawImage raw:
                        sb.Append($" tex={Name(raw.texture)} color={C(raw.color)} mat={Name(raw.material)} uv={raw.uvRect}");
                        if (raw.texture) textures.Add(raw.texture);
                        break;
                    case TMP_Text text:
                        sb.Append($" font={Name(text.font)} fontMat={Name(text.fontSharedMaterial)} size={text.fontSize:0.#} auto={text.enableAutoSizing}({text.fontSizeMin:0.#}-{text.fontSizeMax:0.#}) color={C(text.color)} align={text.alignment} style={text.fontStyle} spacing={text.characterSpacing:0.#} text='{Short(text.text)}'");
                        break;
                    case HorizontalOrVerticalLayoutGroup lg:
                        sb.Append($" spacing={lg.spacing} padding=({lg.padding.left},{lg.padding.right},{lg.padding.top},{lg.padding.bottom}) align={lg.childAlignment} ctrl={lg.childControlWidth}/{lg.childControlHeight} expand={lg.childForceExpandWidth}/{lg.childForceExpandHeight}");
                        break;
                    case GridLayoutGroup gl:
                        sb.Append($" cell={V(gl.cellSize)} spacing={V(gl.spacing)}");
                        break;
                    case LayoutElement le:
                        sb.Append($" min={le.minWidth}x{le.minHeight} pref={le.preferredWidth}x{le.preferredHeight} flex={le.flexibleWidth}x{le.flexibleHeight}");
                        break;
                    case Selectable sel:
                        sb.Append($" transition={sel.transition} normal={C(sel.colors.normalColor)} hl={C(sel.colors.highlightedColor)} pressed={C(sel.colors.pressedColor)} disabled={C(sel.colors.disabledColor)} fade={sel.colors.fadeDuration}");
                        break;
                    case CanvasGroup cg:
                        sb.Append($" alpha={cg.alpha}");
                        break;
                    case Canvas cv:
                        sb.Append($" mode={cv.renderMode} order={cv.sortingOrder} pixelPerfect={cv.pixelPerfect}");
                        break;
                    case CanvasScaler cs:
                        sb.Append($" mode={cs.uiScaleMode} ref={V(cs.referenceResolution)} match={cs.matchWidthOrHeight}");
                        break;
                    case Shadow sh:
                        sb.Append($" color={C(sh.effectColor)} dist={V(sh.effectDistance)}");
                        break;
                    case RoR2.UI.HealthBar hb:
                    {
                        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                        object style = typeof(RoR2.UI.HealthBar).GetField("_style", flags)?.GetValue(hb);
                        object ally = typeof(RoR2.UI.HealthBar).GetField("_allyStyle", flags)?.GetValue(hb);
                        sb.Append($" style={Name(style as Object)} allyStyle={Name(ally as Object)}");
                        break;
                    }
                }
                sb.AppendLine();
            }
            if (level >= maxDepth) return;
            for (int i = 0; i < t.childCount; i++) Walk(sb, t.GetChild(i), level + 1, maxDepth, textures);
        }

        private static string V(Vector2 v) => $"({v.x:0.#},{v.y:0.#})";

        private static string C(Color c) => $"#{ColorUtility.ToHtmlStringRGBA(c)}";

        private static string Short(string s)
        {
            if (s == null) return "";
            s = s.Replace("\n", "\\n");
            return s.Length > 60 ? s.Substring(0, 60) + "..." : s;
        }

        private static void Save(StringBuilder sb, HashSet<Texture> textures)
        {
            string dir = System.IO.Path.Combine(Plugin.PluginDirectory, "screenshots", "ui");
            System.IO.Directory.CreateDirectory(dir);
            int saved = 0;
            foreach (Texture t in textures)
            {
                if (!t) continue;
                try
                {
                    var rt = RenderTexture.GetTemporary(t.width, t.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    Graphics.Blit(t, rt);
                    RenderTexture prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var copy = new Texture2D(t.width, t.height, TextureFormat.RGBA32, false);
                    copy.ReadPixels(new Rect(0, 0, t.width, t.height), 0, 0);
                    copy.Apply();
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, t.name + ".png"), ImageConversion.EncodeToPNG(copy));
                    Object.Destroy(copy);
                    saved++;
                }
                catch (System.Exception e) { sb.AppendLine($"  save {t.name} failed: {e.Message}"); }
            }
            sb.AppendLine($"  saved {saved} textures to {dir}");
        }
    }
}
