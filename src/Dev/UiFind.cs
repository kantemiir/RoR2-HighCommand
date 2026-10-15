using System.Collections.Generic;
using HighCommand.Client;
using HighCommand.UI;
using RoR2;
using UnityEngine;

namespace HighCommand.Dev
{
    /// <summary>
    /// Pointing at the mode's own interface by name rather than by pixels: the corner clusters lean in, so where a
    /// button is on screen depends on the resolution, the scale and the perspective. The pointer still goes through the
    /// mode's input as a mouse would (synthetic input), so a scenario tests the real path.
    /// A name is a GameObject's name (Order_HIGHCOMMAND_CMD_HOLD, Skill_1, Ability0, Mini2, Card, Item, Map ...), with the
    /// canvas in front when the name is on more than one (top:Item), then the number of the one meant (1 = the first).
    /// </summary>
    internal static class UiFind
    {
        private const ConVarFlags HostOnly = ConVarFlags.None;

        [ConCommand(commandName = "hc_ui_where", flags = HostOnly, helpText = "hc_ui_where <name> [n]: prints where a piece of the mode's interface is on screen.")]
        private static void CcWhere(ConCommandArgs args)
        {
            RectTransform rt = Find(args, out string what);
            Vector2 at = ScreenCenter(rt);
            Debug.Log($"HighCommand ui: {what} at ({at.x:0},{at.y:0}) size {rt.rect.width:0}x{rt.rect.height:0} canvas={rt.GetComponentInParent<Canvas>().rootCanvas.name}");
        }

        [ConCommand(commandName = "hc_mouse_ui", flags = HostOnly, helpText = "hc_mouse_ui <name> [n] [shift]: moves the pointer onto a piece of the mode's interface.")]
        private static void CcMouse(ConCommandArgs args)
        {
            Vector2 at = ScreenCenter(Find(args, out string what));
            ClientInput.InjectMove(at, Has(args, "shift"));
            Debug.Log($"HighCommand: pointer on {what} at ({at.x:0},{at.y:0})");
        }

        [ConCommand(commandName = "hc_click_ui", flags = HostOnly, helpText = "hc_click_ui <name> [n] [rmb] [shift]: clicks a piece of the mode's interface (the left button unless rmb).")]
        private static void CcClick(ConCommandArgs args)
        {
            Vector2 at = ScreenCenter(Find(args, out string what));
            int button = Has(args, "rmb") ? 1 : 0;
            ClientInput.InjectClick(at, button, Has(args, "shift"));
            Debug.Log($"HighCommand: click {button} on {what} at ({at.x:0},{at.y:0})");
        }

        private static bool Has(ConCommandArgs args, string flag)
        {
            for (int i = 1; i < args.Count; i++)
                if (string.Equals(args.GetArgString(i), flag, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static Vector2 ScreenCenter(RectTransform rt)
        {
            Vector3 world = rt.TransformPoint(rt.rect.center);
            return RectTransformUtility.WorldToScreenPoint(UiKit.CameraFor(rt), world);
        }

        private static RectTransform Find(ConCommandArgs args, out string what)
        {
            ClientRoot root = ClientRoot.Instance;
            if (!root || root.Layers == null) throw new ConCommandException("Not in a High Command run.");
            if (args.Count < 1) throw new ConCommandException("Give the name of a piece of the interface.");

            string name = args.GetArgString(0);
            // "top:" / "hud:" / "world:" in front: only that canvas (an item cell of the bar and of the item panel share a name)
            Canvas only = null;
            int colon = name.IndexOf(':');
            if (colon > 0)
            {
                string layer = name.Substring(0, colon).ToLowerInvariant();
                only = layer == "top" ? root.Layers.Top : layer == "hud" ? root.Layers.Hud : layer == "world" ? root.Layers.World : null;
                if (!only) throw new ConCommandException("The canvas is top:, hud: or world:.");
                name = name.Substring(colon + 1);
            }
            int nth = args.Count > 1 && int.TryParse(args.GetArgString(1), out int n) && n > 0 ? n : 1;
            what = $"'{args.GetArgString(0)}' {nth}";

            var found = new List<RectTransform>();
            Canvas[] canvases = { root.Layers.Hud, root.Layers.Top, root.Layers.World };
            foreach (Canvas canvas in canvases)
            {
                if (!canvas || (only && canvas != only)) continue;
                foreach (RectTransform rt in canvas.GetComponentsInChildren<RectTransform>(false))
                    if (rt.name == name) found.Add(rt);
            }
            if (found.Count < nth) throw new ConCommandException($"No shown piece {what} ({found.Count} found).");
            return found[nth - 1];
        }
    }
}
