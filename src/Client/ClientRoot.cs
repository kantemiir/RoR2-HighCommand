using System;
using System.Collections.Generic;
using HighCommand.Army;
using HighCommand.Mode;
using HighCommand.UI;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace HighCommand.Client
{
    /// <summary>
    /// Everything that runs on the player's machine while a High Command run is live: the RTS camera, the input gate,
    /// the UI canvas with its cursor, minimap, selection, orders, economy panels and help sheet.
    /// Created by HighCommandRun, destroyed with it.
    /// </summary>
    [DefaultExecutionOrder(10000)] // after the stock camera rig has placed the camera: overlays use this frame's pose
    internal sealed class ClientRoot : MonoBehaviour
    {
        internal static ClientRoot Instance { get; private set; }

        internal RtsCamera Camera { get; private set; }

        internal Selection Selection { get; private set; }

        internal InventoryPanel Inventory => inventory;

        internal OrderController Orders => orders;

        private Canvas canvas;
        private Minimap minimap;
        private HelpOverlay help;
        private WorldOverlay overlay;
        private SelectionController selectionController;
        private OrderController orders;
        private ToastView toast;
        private PurseCluster purse;
        private RunPanel runPanel;
        private ArmyItemStrip itemStrip;
        private DefeatBanner defeat;
        private CommandCard commandCard;
        private AbilityBar abilityBar;
        private RosterPanel roster;
        private BarracksPanel barracks;
        private InventoryPanel inventory;
        private TooltipView tooltip;
        private LootFeed lootFeed;

        private readonly AlertService alerts = new AlertService();
        private bool mapDirty = true;
        private bool focusedThisStage;
        private bool draggingMinimap;

        private static readonly IReadOnlyList<Hero> NoHeroes = new List<Hero>();

        private void Awake()
        {
            Instance = this;
            Camera = gameObject.AddComponent<RtsCamera>();
            Selection = new Selection();

            canvas = UiKit.CreateCanvas("HighCommand.UI", 100);
            canvas.transform.SetParent(transform, false);
            canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            canvas.gameObject.AddComponent<MPEventSystemProvider>();
            canvas.gameObject.AddComponent<MPEventSystemLocator>();
            // The stock cursor opener shows the OS-confined mouse cursor the same way menus do.
            canvas.gameObject.AddComponent<CursorOpener>();

            HudArt.Preload();
            overlay = new WorldOverlay(canvas.transform);   // first: it draws underneath the panels
            // the HUD in the stock HUD's places: minimap, purse, run panel, items, army, orders, abilities
            minimap = new Minimap(canvas.transform);
            purse = new PurseCluster(canvas.transform);
            runPanel = new RunPanel(canvas.transform);
            itemStrip = new ArmyItemStrip(canvas.transform);
            roster = new RosterPanel(canvas.transform);
            commandCard = new CommandCard(canvas.transform);
            abilityBar = new AbilityBar(canvas.transform);
            lootFeed = new LootFeed(canvas.transform);
            defeat = new DefeatBanner(canvas.transform);
            // the windows above the HUD, the messages and the tooltip above everything
            barracks = new BarracksPanel(canvas.transform);
            inventory = new InventoryPanel(canvas.transform);
            help = new HelpOverlay(canvas.transform);
            toast = new ToastView(canvas.transform);
            tooltip = new TooltipView(canvas.transform);
            selectionController = new SelectionController(Selection, overlay);
            orders = new OrderController(Selection, minimap, overlay);
            ClientInput.RegisterUiHitTest(HudContains);

            RegisterHelp();
            Stage.onStageStartGlobal += OnStageStart;
            Economy.OnGoldGained += OnGoldGained;
            ArmyUpgrades.RankedUp += OnRankedUp;
            HighCommand.Orders.HeroBrain.InteractFailed += OnInteractFailed;
        }

        private void OnDestroy()
        {
            Stage.onStageStartGlobal -= OnStageStart;
            Economy.OnGoldGained -= OnGoldGained;
            ArmyUpgrades.RankedUp -= OnRankedUp;
            HighCommand.Orders.HeroBrain.InteractFailed -= OnInteractFailed;
            ClientInput.UnregisterUiHitTest(HudContains);
            minimap?.Destroy();
            help?.Destroy();
            overlay?.Destroy();
            purse?.Destroy();
            runPanel?.Destroy();
            itemStrip?.Destroy();
            defeat?.Destroy();
            roster?.Destroy();
            commandCard?.Destroy();
            abilityBar?.Destroy();
            barracks?.Destroy();
            inventory?.Destroy();
            tooltip?.Destroy();
            lootFeed?.Destroy();
            toast?.Destroy();
            Toast.Clear();
            Feedback.Pings.Clear();
            Feedback.Popups.Clear();
            if (Instance == this) Instance = null;
        }

        private static void RegisterHelp()
        {
            HelpOverlay.Clear();
            HelpOverlay.Section(Loc.Get("HIGHCOMMAND_HELP_SEC_CAMERA"));
            HelpOverlay.Add(
                $"{Settings.KeyUp.Value} {Settings.KeyLeft.Value} {Settings.KeyDown.Value} {Settings.KeyRight.Value}",
                Loc.Get("HIGHCOMMAND_HELP_PAN"));
            HelpOverlay.Add(Loc.Get("HIGHCOMMAND_KEY_EDGE"), Loc.Get("HIGHCOMMAND_HELP_EDGE"));
            HelpOverlay.Add(Loc.Get("HIGHCOMMAND_KEY_WHEEL"), Loc.Get("HIGHCOMMAND_HELP_ZOOM"));
            HelpOverlay.Add($"{Loc.Get("HIGHCOMMAND_KEY_MMB_SIDE")} / {UiKit.KeyText(Settings.KeyRotateLeft.Value)} {UiKit.KeyText(Settings.KeyRotateRight.Value)}", Loc.Get("HIGHCOMMAND_HELP_ROTATE"));
            HelpOverlay.Add($"{Loc.Get("HIGHCOMMAND_KEY_MMB_VERT")} / {UiKit.KeyText(Settings.KeyTiltUp.Value)} {UiKit.KeyText(Settings.KeyTiltDown.Value)}", Loc.Get("HIGHCOMMAND_HELP_TILT"));
            HelpOverlay.Add(Settings.KeyFocusSelected.Value.ToString(), Loc.Get("HIGHCOMMAND_HELP_FOCUS"));
            HelpOverlay.Add(Settings.KeyLastAlert.Value.ToString(), Loc.Get("HIGHCOMMAND_HELP_ALERT"));
            HelpOverlay.Add(Settings.KeyResetCamera.Value.ToString(), Loc.Get("HIGHCOMMAND_HELP_RESETCAM"));
            HelpOverlay.Add(Loc.Get("HIGHCOMMAND_KEY_MINIMAP"), Loc.Get("HIGHCOMMAND_HELP_MINIMAP"));
            HelpOverlay.Section(Loc.Get("HIGHCOMMAND_HELP_SEC_SELECT"));
            HelpOverlay.Add(Loc.Get("HIGHCOMMAND_KEY_LMB"), Loc.Get("HIGHCOMMAND_HELP_SELECT"));
            HelpOverlay.Add("Shift + " + Loc.Get("HIGHCOMMAND_KEY_LMB"), Loc.Get("HIGHCOMMAND_HELP_SELECT_ADD"));
            HelpOverlay.Add(Loc.Get("HIGHCOMMAND_KEY_DOUBLE"), Loc.Get("HIGHCOMMAND_HELP_SELECT_TYPE"));
            HelpOverlay.Add("Ctrl + " + Settings.KeySelectAll.Value, Loc.Get("HIGHCOMMAND_HELP_SELECT_ALL"));
            HelpOverlay.Add("Ctrl + 1..9", Loc.Get("HIGHCOMMAND_HELP_GROUP_SET"));
            HelpOverlay.Add("1..9 (x2)", Loc.Get("HIGHCOMMAND_HELP_GROUP_RECALL"));
            HelpOverlay.Add(Settings.KeyCycleSelection.Value.ToString(), Loc.Get("HIGHCOMMAND_HELP_CYCLE"));
            HelpOverlay.Section(Loc.Get("HIGHCOMMAND_HELP_SEC_GENERAL"));
            HelpOverlay.Add(Settings.KeyHelp.Value.ToString(), Loc.Get("HIGHCOMMAND_HELP_HELP"));
            HelpOverlay.Add("Esc", Loc.Get("HIGHCOMMAND_HELP_PAUSE"));
            HelpOverlay.NextColumn();
            HelpOverlay.Section(Loc.Get("HIGHCOMMAND_HELP_SEC_ORDERS"));
            HelpOverlay.Add(Loc.Get("HIGHCOMMAND_KEY_RMB"), Loc.Get("HIGHCOMMAND_HELP_RMB"));
            HelpOverlay.Add("Shift + " + Loc.Get("HIGHCOMMAND_KEY_RMB"), Loc.Get("HIGHCOMMAND_HELP_QUEUE"));
            HelpOverlay.Add(Loc.Get("HIGHCOMMAND_KEY_RMB_OBJECT"), Loc.Get("HIGHCOMMAND_HELP_LOOT"));
            HelpOverlay.Add(Settings.KeyAttackMove.Value + " + " + Loc.Get("HIGHCOMMAND_KEY_LMB"), Loc.Get("HIGHCOMMAND_HELP_ATTACKMOVE"));
            HelpOverlay.Add(Settings.KeyFocusFire.Value.ToString(), Loc.Get("HIGHCOMMAND_HELP_FOCUSFIRE"));
            HelpOverlay.Add(Settings.KeyStop.Value + " / " + Settings.KeyHold.Value, Loc.Get("HIGHCOMMAND_HELP_STOPHOLD"));
            HelpOverlay.Add(Settings.KeyRetreat.Value + " / " + Settings.KeyPatrol.Value + " / " + Settings.KeyRally.Value, Loc.Get("HIGHCOMMAND_HELP_RETREAT"));
            HelpOverlay.Add(Settings.KeySeekEnemies.Value + " / " + Settings.KeySeekChests.Value, Loc.Get("HIGHCOMMAND_HELP_SEEK"));
            HelpOverlay.Add(Settings.KeyCastSecondary.Value + " " + Settings.KeyCastUtility.Value + " " + Settings.KeyCastSpecial.Value + " " + Settings.KeyCastEquipment.Value, Loc.Get("HIGHCOMMAND_HELP_CAST"));
            HelpOverlay.Add(Settings.KeyToggleAuto.Value + " + Z/X/C/Y", Loc.Get("HIGHCOMMAND_HELP_AUTO"));
            HelpOverlay.Add(Settings.KeyStanceAggressive.Value + " / " + Settings.KeyStanceDefensive.Value + " / " + Settings.KeyStancePassive.Value, Loc.Get("HIGHCOMMAND_HELP_STANCE"));
            HelpOverlay.Add(Settings.KeyFormationCycle.Value.ToString(), Loc.Get("HIGHCOMMAND_HELP_FORMATION"));
            HelpOverlay.Section(Loc.Get("HIGHCOMMAND_HELP_SEC_ARMY"));
            HelpOverlay.Add(Settings.KeyBarracks.Value.ToString(), Loc.Get("HIGHCOMMAND_HELP_BARRACKS"));
            HelpOverlay.Add(Settings.KeyInventory.Value.ToString(), Loc.Get("HIGHCOMMAND_HELP_INVENTORY"));
            HelpOverlay.Add(Settings.KeyRevive.Value.ToString(), Loc.Get("HIGHCOMMAND_HELP_REVIVE"));
            HelpOverlay.Add(Settings.KeyTeleporter.Value.ToString(), Loc.Get("HIGHCOMMAND_HELP_TELEPORTER"));
            HelpOverlay.Add($"{Settings.KeyAbility[0].Value} {Settings.KeyAbility[1].Value} {Settings.KeyAbility[2].Value} {Settings.KeyAbility[3].Value}",
                Loc.Get("HIGHCOMMAND_HELP_ABILITIES"));
        }

        /// <summary>True when the Esc / pause key is intercepted by a Harmony patch (so Esc closes our panels first).</summary>
        internal static bool EscapeGuardActive;

        /// <summary>
        /// Esc closes the topmost thing of the mode: a pending targeting mode, the help sheet, the item panel or the
        /// recruiting panel. Returns true when something was closed (then the game must not open its pause menu).
        /// </summary>
        internal bool ConsumeEscape()
        {
            if (orders != null && orders.Mode != OrderController.Pending.None) { orders.CancelMode(); return true; }
            if (help != null && help.Visible) { help.Hide(); return true; }
            if (inventory != null && inventory.Visible) { inventory.Hide(); return true; }
            if (barracks != null && barracks.Visible) { barracks.Hide(); return true; }
            return false;
        }

        /// <summary>Testing: what the pointer badge shows now.</summary>
        internal OrderController.CursorInfo CursorInfo => orders != null ? orders.Cursor : default;

        /// <summary>Testing: which panels are open and which targeting mode waits for a click.</summary>
        internal string Describe()
        {
            return $"mode={orders.Mode} help={help.Visible} barracks={barracks.Visible} inventory={inventory.Visible} " +
                   $"selected={Selection.Count} scale={appliedUiScale:0.00} screen={Screen.width}x{Screen.height}";
        }

        private CanvasGroup canvasGroup;

        private float appliedUiScale = -1f;
        private float hudScale = 1f;
        private float hudScaleReadAt = -10f;

        // The whole interface is laid out for 1920x1080. The game's own HUD scale setting (hud_scale) applies to it as to
        // the stock HUD, and Interface/UiScale comes on top; both shrink or grow the reference area.
        private void ApplyUiScale()
        {
            if (Time.unscaledTime - hudScaleReadAt > 1f)
            {
                hudScaleReadAt = Time.unscaledTime;
                hudScale = GameHudScale();
            }
            float scale = Mathf.Clamp(Settings.UiScale.Value * hudScale, 0.5f, 1.8f);
            if (Mathf.Approximately(scale, appliedUiScale)) return;
            appliedUiScale = scale;
            var scaler = canvas ? canvas.GetComponent<UnityEngine.UI.CanvasScaler>() : null;
            if (scaler) scaler.referenceResolution = new Vector2(1920f, 1080f) / scale;
        }

        private static float GameHudScale()
        {
            try
            {
                var convar = RoR2.Console.instance ? RoR2.Console.instance.FindConVar("hud_scale") : null;
                if (convar != null && float.TryParse(convar.GetString(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float percent) && percent > 0f)
                    return Mathf.Clamp(percent / 100f, 0.5f, 1.5f);
            }
            catch (Exception) { /* the stock setting is optional */ }
            return 1f;
        }

        /// <summary>Testing: hides / shows the whole interface (measures what drawing it costs).</summary>
        internal void SetUiHidden(bool hidden)
        {
            if (canvas) canvas.gameObject.SetActive(!hidden);
        }

        private void OnStageStart(Stage stage)
        {
            mapDirty = true;
            CameraGround.Clear(); // the old stage's ground until the new one is read
            focusedThisStage = false;
            Scouting.Reset();
            Interactables.ResetForStage();
        }

        private static void OnGoldGained(uint amount, Vector3? at)
        {
            if (at.HasValue) Feedback.Popup("+$" + amount, at.Value, UiKit.Gold);
        }

        private static void OnRankedUp(Hero hero)
        {
            Toast.Show(Loc.Format("HIGHCOMMAND_RANK_UP", hero.DisplayName, hero.Rank));
            CharacterBody body = hero.Body;
            if (body) Feedback.Popup(new string('★', hero.Rank), body.corePosition + Vector3.up * 2f, UiKit.Gold);
        }

        private static void OnInteractFailed(Hero hero, GameObject target, HighCommand.Orders.InteractFailure why)
        {
            string name = hero != null ? hero.DisplayName : "?";
            switch (why)
            {
                case HighCommand.Orders.InteractFailure.NoGold:
                    Interactables.TryGetPrice(target, out CostTypeIndex _, out int cost);
                    Toast.Error(Loc.Format("HIGHCOMMAND_LOOT_FAILED_GOLD", name, cost));
                    break;
                case HighCommand.Orders.InteractFailure.CannotPay:
                    Toast.Error(Loc.Format("HIGHCOMMAND_LOOT_FAILED_PRICE", name));
                    break;
                case HighCommand.Orders.InteractFailure.Unreachable:
                    Toast.Error(Loc.Format("HIGHCOMMAND_LOOT_FAILED_REACH", name));
                    break;
            }
        }

        private IReadOnlyList<Hero> CurrentHeroes()
        {
            HcState state = HcState.Instance;
            return state != null ? state.Heroes : NoHeroes;
        }

        private void Update()
        {
            long perfStart = Dev.Perf.Begin();
            Dev.Perf.Frame();
            ClientInput.Refresh();
            ApplyUiScale();

            // The pause screen and the console are drawn below this canvas. The pause menu and its settings fill the
            // screen: the mode's interface is hidden while they are up (dimmed, it still covered their tabs and rows).
            // The console is a small window: the interface is only dimmed under it.
            bool pauseMenu = PauseManager.isPaused || PauseManager.PauseScreenIsOpen();
            float wantAlpha = pauseMenu ? 0f : ClientInput.MouseBlocked ? 0.35f : 1f;
            if (canvasGroup && !Mathf.Approximately(canvasGroup.alpha, wantAlpha)) canvasGroup.alpha = wantAlpha;

            // The map picture is built once the stage's node graph exists.
            if (mapDirty && SceneInfo.instance && SceneInfo.instance.groundNodes)
            {
                mapDirty = false;
                try
                {
                    MapInfo.Rebuild();
                    minimap.OnMapChanged();
                }
                catch (Exception e) { Plugin.Log.LogError("MapInfo.Rebuild failed: " + e); }
            }

            // Show the interface only while a stage is running (not in loading screens / game over).
            bool inStage = Stage.instance != null && Run.instance != null && !Run.instance.isGameOverServer;
            minimap.SetVisible(inStage);
            purse.SetVisible(inStage);
            runPanel.SetVisible(inStage);
            itemStrip.SetVisible(inStage);

            if (!focusedThisStage && inStage && Camera)
                TryInitialFocus();

            // Esc closes the topmost thing of the mode. Normally the pause key is intercepted (see EscapeGuard) so that
            // the same press does not also open the pause menu; this is the fallback when that patch is missing.
            // (a test command's Esc never reaches the game's pause key, so it is handled here in any case)
            if ((!EscapeGuardActive && ClientInput.RawKeyDown(KeyCode.Escape) && !ClientInput.KeyboardBlocked)
                || ClientInput.InjectedKeyDown(KeyCode.Escape)) ConsumeEscape();

            if (ClientInput.KeyDown(Settings.KeyHelp.Value)) Open(WindowKind.Help);
            if (inStage)
            {
                if (ClientInput.KeyDown(Settings.KeyBarracks.Value)) Open(WindowKind.Barracks);
                if (ClientInput.KeyDown(Settings.KeyInventory.Value)) Open(WindowKind.Items);

                if (ClientInput.MouseDown(0))
                {
                    Vector2 at = ClientInput.Mouse;
                    if (purse.Contains(at)) Open(WindowKind.Barracks);
                    else if (itemStrip.Contains(at)) Open(WindowKind.Items);
                    else if (runPanel.ObjectivesContain(at)) orders.TeleporterAction();
                    else if (defeat.Contains(at)) orders.Revive(null);
                }
            }
            else
            {
                barracks.Hide();
                inventory.Hide();
            }

            // A click beside an open window, in the world, only closes it, as in any menu: it would also have deselected
            // the army or sent it somewhere. (A click on the HUD still does its job there.)
            if (AnyWindow && !ClientInput.PointerOverUi)
            {
                for (int b = 0; b < 2; b++)
                {
                    if (!ClientInput.MouseDown(b)) continue;
                    CloseWindows();
                    ClientInput.Swallow(b);
                }
            }

            IReadOnlyList<Hero> heroes = CurrentHeroes();
            bool inputAllowed = inStage && !PauseManager.isPaused && !help.Visible;

            if (Camera && inStage)
            {
                HandleMinimapMouse();
                if (ClientInput.KeyDown(Settings.KeyFocusSelected.Value)) FocusSelectionOrArmy();
                if (alerts.HasAlert && ClientInput.KeyDown(Settings.KeyLastAlert.Value)) Camera.FocusOn(alerts.LastPosition);
            }
            if (inStage)
            {
                alerts.Tick(heroes);
                Scouting.Tick(heroes, Camera);
            }
            else alerts.Reset();

            long t = Dev.Perf.Begin();
            orders.Tick(heroes, inputAllowed);
            Dev.Perf.End(ref Dev.Perf.Part[Dev.Perf.PartOrders], t);

            t = Dev.Perf.Begin();
            selectionController.Tick(heroes, Camera, inputAllowed);
            orders.UpdateHover(heroes, inputAllowed && Camera && Camera.SceneCam);
            Dev.Perf.End(ref Dev.Perf.Part[Dev.Perf.PartPick], t);

            // every panel on its own: one that fails is reported once and the others (and the stock HUD) go on
            t = Dev.Perf.Begin();
            try { purse.Tick(Time.unscaledDeltaTime); } catch (Exception e) { Failed(PanelPurse, e); }
            try { runPanel.Tick(); } catch (Exception e) { Failed(PanelRun, e); }
            try { itemStrip.Tick(); } catch (Exception e) { Failed(PanelItems, e); }
            try { defeat.Tick(inStage); } catch (Exception e) { Failed(PanelDefeat, e); }
            Dev.Perf.End(ref Dev.Perf.Part[Dev.Perf.PartTopBar], t);

            t = Dev.Perf.Begin();
            try { barracks.Tick(); } catch (Exception e) { Failed(PanelBarracks, e); }
            try { inventory.Tick(); } catch (Exception e) { Failed(PanelInventory, e); }
            try { help.Tick(); } catch (Exception e) { Failed(PanelHelp, e); }
            Dev.Perf.End(ref Dev.Perf.Part[Dev.Perf.PartOther], t);

            t = Dev.Perf.Begin();
            try { roster.Tick(Selection, orders, inStage); } catch (Exception e) { Failed(PanelRoster, e); }
            Dev.Perf.End(ref Dev.Perf.Part[Dev.Perf.PartRoster], t);

            t = Dev.Perf.Begin();
            try { commandCard.Tick(Selection, orders, inStage); } catch (Exception e) { Failed(PanelCommand, e); }
            try
            {
                AbilityBar.Screen clicked = abilityBar.Tick(orders, inStage, barracks.Visible, inventory.Visible, help.Visible);
                if (clicked == AbilityBar.Screen.Barracks) Open(WindowKind.Barracks);
                else if (clicked == AbilityBar.Screen.Items) Open(WindowKind.Items);
                else if (clicked == AbilityBar.Screen.Help) Open(WindowKind.Help);
            }
            catch (Exception e) { Failed(PanelAbilities, e); }
            Dev.Perf.End(ref Dev.Perf.Part[Dev.Perf.PartCommand], t);

            t = Dev.Perf.Begin();
            try
            {
                lootFeed.SetTop(runPanel.Bottom);
                lootFeed.Tick();
            }
            catch (Exception e) { Failed(PanelLoot, e); }
            try { toast.Tick(); } catch (Exception e) { Failed(PanelToast, e); }
            if (ClientInput.MouseBlocked) Tooltip.Clear(); // a pause screen or the console is on top: no tooltips
            try { tooltip.Tick(); } catch (Exception e) { Failed(PanelTooltip, e); } // last: shows what the panels asked for
            Dev.Perf.End(ref Dev.Perf.Part[Dev.Perf.PartOther], t);
            Dev.Perf.End(ref Dev.Perf.Ui, perfStart);
        }

        // ---- windows ---------------------------------------------------------------------------------------------

        private enum WindowKind { Barracks, Items, Help }

        private bool AnyWindow => barracks.Visible || inventory.Visible || help.Visible;

        /// <summary>Opens a window (or closes it when it is the one open): one window at a time.</summary>
        private void Open(WindowKind which)
        {
            bool wasOpen = which == WindowKind.Barracks ? barracks.Visible : which == WindowKind.Items ? inventory.Visible : help.Visible;
            CloseWindows();
            if (wasOpen) return;
            switch (which)
            {
                case WindowKind.Barracks: barracks.Show(); break;
                case WindowKind.Items: inventory.Show(); break;
                default: help.Toggle(); break;
            }
        }

        private void CloseWindows()
        {
            barracks.Hide();
            inventory.Hide();
            help.Hide();
        }

        // the HUD pieces that have no hit test of their own: a click on them must not reach the world
        private bool HudContains(Vector2 point) =>
            purse.Contains(point) || itemStrip.Contains(point) || runPanel.ObjectivesContain(point) || defeat.Contains(point)
            || help.Contains(point);

        // ---- one failing panel does not take the rest down -------------------------------------------------------

        private const int PanelPurse = 0, PanelRun = 1, PanelItems = 2, PanelDefeat = 3, PanelBarracks = 4, PanelInventory = 5,
            PanelHelp = 6, PanelRoster = 7, PanelCommand = 8, PanelAbilities = 9, PanelLoot = 10, PanelToast = 11, PanelTooltip = 12;
        private static readonly string[] PanelNames =
            { "purse", "run panel", "items", "defeat", "barracks", "inventory", "help", "roster", "command card", "abilities", "loot feed", "toast", "tooltip" };
        private readonly bool[] panelFailed = new bool[PanelNames.Length];

        private void Failed(int panel, Exception e)
        {
            if (panelFailed[panel]) return;
            panelFailed[panel] = true; // reported once: an error every frame would flood the log
            Plugin.Log.LogError($"HighCommand UI: the {PanelNames[panel]} failed (the rest goes on): {e}");
        }

        // Overlays use the camera pose of this very frame (this component runs after the stock camera rig).
        private void LateUpdate()
        {
            bool inStage = Stage.instance != null && Run.instance != null && !Run.instance.isGameOverServer;
            long t = Dev.Perf.Begin();
            // (rings and bars would lie over the pause menu / console, so they are not drawn while those are up)
            if (inStage && !ClientInput.MouseBlocked && Camera && Camera.SceneCam)
                overlay.Draw(Camera, Selection, CurrentHeroes(), orders.Cursor);
            else overlay.Draw(null, Selection, CurrentHeroes());
            Dev.Perf.End(ref Dev.Perf.Part[Dev.Perf.PartOverlay], t);

            t = Dev.Perf.Begin();
            minimap.Tick(Camera, Selection, Time.unscaledDeltaTime);
            Dev.Perf.End(ref Dev.Perf.Part[Dev.Perf.PartMinimap], t);
        }

        // the first look at a stage: about 31 m, as before the far zoom (MaxDistance 220) existed
        private const float StartDistance = 31f;

        /// <summary>Looks at the army when a stage begins (first friendly body appears).</summary>
        private void TryInitialFocus()
        {
            // The first hero arrives in a drop pod from high above the map: wait for the landing and, meanwhile, look at
            // the ground below the pod (a camera at the pod would show only sky).
            Vector3? where = ArmyCenter(requireOnFoot: true);
            if (!where.HasValue)
            {
                Vector3? falling = ArmyCenter();
                if (falling.HasValue)
                {
                    Camera.FocusOn(HighCommand.Orders.Formation.SnapToGround(falling.Value), snap: true);
                    Camera.SetDistance(StartDistance);
                }
                return;
            }
            Camera.FocusOn(where.Value, snap: true);
            Camera.SetDistance(StartDistance);
            focusedThisStage = true;
            orders.RallyPoint = where.Value;

            // the army is ready for orders: nobody has to click on the first hero before commanding
            if (Selection.Count == 0)
            {
                IReadOnlyList<Hero> heroes = CurrentHeroes();
                if (heroes.Count > 0) Selection.Set(heroes);
            }
        }

        private void HandleMinimapMouse()
        {
            if (!ClientInput.MouseHeld(0)) { draggingMinimap = false; return; }

            bool pressed = ClientInput.MouseDown(0);
            if (pressed && minimap.Contains(ClientInput.Mouse)) draggingMinimap = true;
            if (!draggingMinimap) return;

            Vector3? world = minimap.ScreenToWorld(ClientInput.Mouse);
            if (world.HasValue) Camera.PanTo(world.Value);
        }

        private float lastFocusPress = -10f;

        // One press flies to the selected heroes (or the army); two quick presses lock the camera on them, as the follow
        // camera of Warcraft III / StarCraft II, until the player pans by hand or presses once more.
        private void FocusSelectionOrArmy()
        {
            float now = Time.unscaledTime;
            bool twice = now - lastFocusPress < 0.35f;
            lastFocusPress = now;
            if (twice)
            {
                Camera.FollowTarget = () => Selection.Center() ?? ArmyCenter();
                Toast.Show(Loc.Get("HIGHCOMMAND_CAMERA_FOLLOW"));
                return;
            }
            if (Camera.IsFollowing) Camera.FollowTarget = null;
            Vector3? where = Selection.Center() ?? ArmyCenter();
            if (where.HasValue) Camera.FocusOn(where.Value);
        }

        /// <summary>Average position of living friendly bodies (hero bodies first when the roster is known).</summary>
        internal static Vector3? ArmyCenter(bool requireOnFoot = false)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            var bodies = CharacterBody.readOnlyInstancesList;
            for (int pass = 0; pass < 2 && count == 0; pass++)
            {
                for (int i = 0; i < bodies.Count; i++)
                {
                    CharacterBody b = bodies[i];
                    if (!b || !b.healthComponent || !b.healthComponent.alive || !b.teamComponent) continue;
                    if (b.teamComponent.teamIndex != TeamIndex.Player) continue;
                    if (requireOnFoot && (b.currentVehicle || (b.characterMotor && !b.characterMotor.isGrounded))) continue;
                    if (pass == 0 && HeroMarker.Of(b) == null) continue; // server knows the heroes; clients fall through
                    sum += b.corePosition;
                    count++;
                }
            }
            return count > 0 ? sum / count : (Vector3?)null;
        }
    }
}
