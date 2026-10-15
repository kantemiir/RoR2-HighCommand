using System.Reflection;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HighCommand.UI
{
    /// <summary>
    /// Asks for the game's own item card (the inspect panel the scoreboard opens on a click on an item): a click on an item
    /// of the army anywhere in the interface calls <see cref="Show(ItemIndex)"/>; <see cref="ItemCardView"/> shows it.
    /// </summary>
    internal static class ItemCard
    {
        private static PickupIndex requested = PickupIndex.none;

        internal static void Show(ItemIndex item)
        {
            if (item != ItemIndex.None) requested = PickupCatalog.FindPickupIndex(item);
        }

        internal static void Show(EquipmentIndex equipment)
        {
            if (equipment != EquipmentIndex.None) requested = PickupCatalog.FindPickupIndex(equipment);
        }

        internal static bool TakeRequest(out PickupIndex pickup)
        {
            pickup = requested;
            requested = PickupIndex.none;
            return pickup != PickupIndex.none;
        }

        internal static void Clear() => requested = PickupIndex.none;
    }

    /// <summary>
    /// The game's inspect panel, hosted as the scoreboard hosts it: the big icon, the name in the tier colour, the full
    /// description. Any click or Esc closes it (the caller does that). It is opened without the game's inspect pause: the
    /// mode's interface steps aside while the game is paused, and the army goes on fighting while one reads.
    /// </summary>
    internal sealed class ItemCardView
    {
        private const string PrefabKey = "RoR2/Base/UI/InspectPanel.prefab";
        private const string PanelPath = "InspectPanelArea/InspectionPanel";   // the visible box of the card

        private static readonly FieldInfo AutopauseField =
            typeof(InspectPanelController).GetField("isAutopauseCandidate", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly GameObject instance;
        private readonly InspectPanelController controller;
        private readonly RectTransform area;
        private readonly RectTransform box;
        private bool open;
        private bool failed;

        internal ItemCardView(Transform parent)
        {
            try
            {
                GameObject prefab = Addressables.LoadAssetAsync<GameObject>(PrefabKey).WaitForCompletion();
                if (!prefab) { Plugin.Log.LogWarning("ItemCard: the game has no item card (" + PrefabKey + ")."); return; }
                instance = Object.Instantiate(prefab, parent, false);
                instance.name = "ItemCard";
                controller = instance.GetComponent<InspectPanelController>();
                area = instance.transform.Find("InspectPanelArea") as RectTransform;
                box = instance.transform.Find(PanelPath) as RectTransform;
                if (controller && AutopauseField != null) AutopauseField.SetValue(controller, false);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("ItemCard: the game's item card could not be made: " + e.Message);
                if (instance) Object.Destroy(instance);
                instance = null;
                controller = null;
            }
        }

        internal bool Visible => open && area && area.gameObject.activeInHierarchy;

        /// <summary>The card's box (a click there must not reach what lies under it).</summary>
        internal bool Contains(Vector2 screenPoint) => Visible && UiKit.Contains(box ? box : area, screenPoint);

        /// <summary>The name of the item shown (testing), or null.</summary>
        internal string ShownName { get; private set; }

        internal void Tick()
        {
            if (!ItemCard.TakeRequest(out PickupIndex pickup)) return;
            Show(pickup);
        }

        private void Show(PickupIndex pickup)
        {
            PickupDef def = PickupCatalog.GetPickupDef(pickup);
            if (def == null || !controller || failed) return;
            try
            {
                InspectInfo info = def;
                // the army's items come in through the heroes, so the commander's profile never "discovers" them: shown
                // as they are, not as an unknown item
                info.MarkForceShowInfo();
                instance.transform.SetAsLastSibling();   // over a window that came to the front when it opened
                controller.Show(info);
                open = true;
                ShownName = Language.GetString(def.nameToken);
                UiSound.Click();
            }
            catch (System.Exception e)
            {
                failed = true;   // reported once; the tooltips still tell what an item does
                Plugin.Log.LogWarning("ItemCard: the game's item card could not be shown: " + e);
            }
        }

        internal void Hide()
        {
            if (!open) return;
            open = false;
            ShownName = null;
            if (controller) controller.OnCloseButtonClicked();
        }

        internal void Destroy()
        {
            if (instance) Object.Destroy(instance);
        }
    }
}
