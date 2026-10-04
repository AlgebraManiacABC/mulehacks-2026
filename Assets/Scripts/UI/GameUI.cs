using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameUI : MonoBehaviour
{
    private enum Pending { NONE, SETTLER_SOURCE, ROAD_TARGET, SHIP_TARGET, SHIP_AMOUNTS }

    private const int FontSize = 16;

    private TurnManager tm;
    private HexTile selected;
    private Pending pending;
    private Settlement shipTo;
    private readonly Dictionary<ResourceData, int> cargo = new();
    private readonly List<Rect> uiRects = new();
    private Vector2 panelScroll, projectScroll;
    // Game actions clicked in OnGUI run on the next Update so the GUI layout stays consistent
    private System.Action queued;

    private Settlement SelectedSettlement => selected != null ? selected.settlement : null;

    private void Start()
    {
        tm = TurnManager.Instance;
    }

    private void Update()
    {
        queued?.Invoke();
        queued = null;
        if (tm.state != GameState.PLAYING) return;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.enterKey.wasPressedThisFrame) tm.AdvanceTurn();
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (pending != Pending.NONE) pending = Pending.NONE;
                else Select(null);
            }
        }

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
        Vector2 pos = mouse.position.ReadValue();
        Vector2 guiPos = new Vector2(pos.x, Screen.height - pos.y);
        foreach (var r in uiRects)
            if (r.Contains(guiPos)) return;

        if (!Physics.Raycast(Camera.main.ScreenPointToRay(pos), out var hit)) return;
        var tile = hit.collider.GetComponentInParent<HexTile>();
        if (tile == null) return;

        var from = SelectedSettlement;
        if (pending == Pending.ROAD_TARGET && from != null && tile.settlement != null)
        {
            tm.BuildRoad(from, tile.settlement);
            pending = Pending.NONE;
        }
        else if (pending == Pending.SHIP_TARGET && from != null && tile.settlement != null
                 && tm.CanShip(from, tile.settlement, out _, out _))
        {
            BeginShipment(tile.settlement);
        }
        else
        {
            Select(tile);
        }
    }

    private void Select(HexTile tile)
    {
        selected = tile;
        pending = Pending.NONE;
        HexWorldManager.Instance.Highlight(tile);
    }

    private void BeginShipment(Settlement to)
    {
        shipTo = to;
        cargo.Clear();
        pending = Pending.SHIP_AMOUNTS;
    }

    private void OnGUI()
    {
        if (Event.current.type == EventType.Layout) uiRects.Clear();
        GUI.skin.label.wordWrap = true;
        GUI.skin.label.richText = true;
        GUI.skin.label.fontSize = GUI.skin.button.fontSize = GUI.skin.box.fontSize = FontSize;

        if (tm.state != GameState.PLAYING)
        {
            DrawEndScreen();
            return;
        }

        DrawTopBar();
        DrawProjects();
        if (selected != null) DrawTilePanel();
    }

    private Rect Area(Rect rect)
    {
        if (Event.current.type == EventType.Layout) uiRects.Add(rect);
        GUILayout.BeginArea(rect, GUI.skin.box);
        return rect;
    }

    private void DrawTopBar()
    {
        Area(new Rect(10, 10, Screen.width - 20, 44));
        GUILayout.BeginHorizontal();
        var capital = tm.capital;
        GUILayout.Label("Turn " + tm.currentTurn
                        + "    Capital T" + capital.tier + "/" + tm.winTier
                        + "    Capital pop " + capital.population
                        + "    Settlements " + tm.settlements.Count
                        + "    Settler groups " + tm.activeSettlers.Count);
        if (GUILayout.Button("Next Turn (Enter)", GUILayout.Width(190), GUILayout.Height(32))) queued = tm.AdvanceTurn;
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void DrawProjects()
    {
        Area(new Rect(Screen.width - 350, 64, 340, 300));
        GUILayout.Label("<b>In progress</b>");
        projectScroll = GUILayout.BeginScrollView(projectScroll);
        foreach (var p in tm.projects)
            GUILayout.Label(p.name + " (" + p.turnsLeft + " turns)");
        foreach (var g in tm.activeSettlers)
            GUILayout.Label(g.settlers + " settlers " + g.origin + " -> " + g.destination + " (" + g.turnsLeft + " turns)");
        if (tm.projects.Count == 0 && tm.activeSettlers.Count == 0) GUILayout.Label("Nothing");
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawTilePanel()
    {
        Area(new Rect(10, 64, 420, Screen.height - 74));
        panelScroll = GUILayout.BeginScrollView(panelScroll);

        string resource = selected.resource != null
            ? selected.resource.resourceName + (selected.resource.isPrimary ? " (primary)" : "")
            : "none";
        GUILayout.Label("<b>Tile " + selected + "</b>  " + selected.biome.biomeName + ", resource: " + resource);

        var s = SelectedSettlement;
        if (s == null) DrawEmptyTileActions();
        else DrawSettlementActions(s);

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawEmptyTileActions()
    {
        if (pending != Pending.SETTLER_SOURCE)
        {
            if (ActionButton("Send settlers here...", tm.settlements.Count > 0, null)) pending = Pending.SETTLER_SOURCE;
            return;
        }

        GUILayout.Label("Send " + tm.settlerCount + " settlers from (costs " + tm.settlerResourceCost + "):");
        foreach (var from in tm.settlements)
        {
            bool ok = tm.CanSendSettlers(from, selected, out string why);
            if (ActionButton(from.DisplayName + " - " + tm.SettlerTurns(from, selected) + " turns", ok, why))
            {
                var to = selected;
                queued = () => tm.SendSettlers(from, to);
                pending = Pending.NONE;
            }
        }
        if (GUILayout.Button("Cancel")) pending = Pending.NONE;
    }

    private void DrawSettlementActions(Settlement s)
    {
        GUILayout.Label(s.DisplayName + "  T" + s.tier
                        + "\nPopulation " + s.population + "/" + s.MaxPopulation
                        + "\nFarm: " + (s.hasFarm ? "yes" : "no") + "   Mill: " + (s.millTier > 0 ? "T" + s.millTier : "no")
                        + "\nResources: " + s.resources);

        switch (pending)
        {
            case Pending.ROAD_TARGET:
                GUILayout.Label("Click another settlement to build/upgrade a road to:");
                foreach (var other in tm.settlements)
                {
                    if (other == s) continue;
                    bool ok = tm.CanBuildRoad(s, other, out int turns, out string why);
                    if (ActionButton(other.DisplayName + (ok ? " - " + turns + " turns" : ""), ok, why))
                    {
                        var target = other;
                        queued = () => tm.BuildRoad(s, target);
                        pending = Pending.NONE;
                    }
                }
                if (GUILayout.Button("Cancel")) pending = Pending.NONE;
                return;

            case Pending.SHIP_TARGET:
                GUILayout.Label("Click a destination settlement:");
                foreach (var other in tm.settlements)
                {
                    if (other == s) continue;
                    bool ok = tm.CanShip(s, other, out int turns, out string why);
                    if (ActionButton(other.DisplayName + (ok ? " - " + turns + " turns" : ""), ok, why)) BeginShipment(other);
                }
                if (GUILayout.Button("Cancel")) pending = Pending.NONE;
                return;

            case Pending.SHIP_AMOUNTS:
                DrawCargo(s);
                return;
        }

        GUILayout.Space(8);
        if (s.isCapital)
        {
            bool ok = tm.CanUpgradeCity(s, out string why);
            if (ActionButton("Upgrade city to T" + (s.tier + 1) + " (" + tm.UpgradeTurns(s) + " turns, " + tm.UpgradeCost(s) + ")", ok, why))
                queued = () => tm.UpgradeCity(s);
        }

        if (!s.hasFarm)
        {
            bool ok = tm.CanBuildFarm(s, out string why);
            if (ActionButton("Build farm (1 turn, " + tm.farmCost + ")", ok, why)) queued = () => tm.BuildFarm(s);
        }

        {
            bool ok = tm.CanBuildMill(s, out string why);
            string label = (s.millTier == 0 ? "Build mill" : "Upgrade mill to T" + (s.millTier + 1))
                           + " (" + tm.MillTurns(s) + " turns, " + tm.MillCost(s) + ")";
            if (ActionButton(label, ok, why)) queued = () => tm.BuildMill(s);
        }

        bool others = tm.settlements.Count > 1;
        if (ActionButton("Build/upgrade road...", others && !tm.IsBusy(s, ProjectType.ROAD),
                others ? "Road already under construction" : "No other settlements"))
            pending = Pending.ROAD_TARGET;
        if (ActionButton("Send resources...", others && s.resources.resources.Count > 0,
                others ? "Nothing to send" : "No other settlements"))
            pending = Pending.SHIP_TARGET;
    }

    private void DrawCargo(Settlement from)
    {
        if (shipTo == null || !tm.CanShip(from, shipTo, out int turns, out _))
        {
            GUILayout.Label("Destination is no longer reachable.");
            if (GUILayout.Button("Cancel")) pending = Pending.NONE;
            return;
        }
        GUILayout.Label("Send to " + shipTo.DisplayName + " (" + turns + " turns):");
        var shipment = new ResourceCollection { resources = new List<ResourcePile>() };
        foreach (var pile in from.resources.resources)
        {
            cargo.TryGetValue(pile.resource, out int amount);
            GUILayout.BeginHorizontal();
            GUILayout.Label(pile.resource.resourceName + " (" + pile.amount + ")", GUILayout.Width(150));
            if (GUILayout.Button("-5", GUILayout.Width(40))) amount -= 5;
            GUILayout.Label(amount.ToString(), GUILayout.Width(44));
            if (GUILayout.Button("+5", GUILayout.Width(40))) amount += 5;
            if (GUILayout.Button("All", GUILayout.Width(50))) amount = pile.amount;
            GUILayout.EndHorizontal();
            amount = Mathf.Clamp(amount, 0, pile.amount);
            cargo[pile.resource] = amount;
            shipment.Add(pile.resource, amount);
        }

        if (ActionButton("Send", shipment.resources.Count > 0, "Choose something to send"))
        {
            var to = shipTo;
            queued = () => tm.Ship(from, to, shipment);
            pending = Pending.NONE;
        }
        if (GUILayout.Button("Cancel")) pending = Pending.NONE;
    }

    // A button that is grayed out with the reason shown when unavailable
    private static bool ActionButton(string label, bool enabled, string why)
    {
        bool wasEnabled = GUI.enabled;
        GUI.enabled = enabled;
        bool clicked = GUILayout.Button(label);
        GUI.enabled = wasEnabled;
        if (!enabled && !string.IsNullOrEmpty(why)) GUILayout.Label("  <i>" + why + "</i>");
        return clicked && enabled;
    }

    private void DrawEndScreen()
    {
        var rect = new Rect(Screen.width / 2f - 210, Screen.height / 2f - 100, 420, 200);
        GUILayout.BeginArea(rect, GUI.skin.box);
        GUILayout.Label(tm.state == GameState.WON
            ? "<b>Victory!</b>\nYour capital reached tier " + tm.winTier + " on turn " + tm.currentTurn + "."
            : "<b>Defeat.</b>\nYour capital was lost on turn " + tm.currentTurn + ".");
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Play again")) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        if (GUILayout.Button("Main menu")) SceneManager.LoadScene("MainMenu");
        GUILayout.EndArea();
    }
}
