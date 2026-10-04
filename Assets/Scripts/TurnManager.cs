using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public enum ProjectType { CITY_UPGRADE, FARM, MILL, ROAD, SHIPMENT }

public enum GameState { PLAYING, WON, LOST }

public class Project
{
    public string name;
    public ProjectType type;
    public Settlement owner;
    public int turnsLeft;
    public Action onComplete;
    public RouteMover mover;
}

/**
 * Deals with anything turn-related.
 * Also places the initial embark.
 */
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;
    // The resources every civilization starts out with
    [SerializeField]
    public ResourcePile[] embarkResources;
    // The first dwelling to place on an embark
    [SerializeField]
    public Building embarkBuilding;
    // Upgrade costs and times of the capital, indexed by target tier
    [SerializeField]
    public Building capitolBuilding;
    public Building farmBuilding;
    public Building millBuilding;
    public GameObject settlerPrefab;
    public GameObject caravanPrefab;
    // The HexWorldManager, for ease of communication
    [SerializeField]
    public HexWorldManager hexWorldManager;
    // The amount of resources it takes to send out a settler group
    public ResourceCollection settlerResourceCost;
    // How many people leave with a settler group
    public int settlerCount = 2;
    public ResourceCollection farmCost;
    public int farmYield = 6;
    // Multiplied by the target mill tier
    public ResourceCollection millCostPerTier;
    public int maxMillTier = 4;
    // How many times a mill runs each conversion per turn, per mill tier
    public int millRunsPerTier = 3;
    public List<ResourceConversion> millConversions;
    // Building the capital to this tier wins the game
    public int winTier = 7;
    // A list of settlers who have ventured forth
    public List<SettlerGroup> activeSettlers;

    public List<Settlement> settlements = new();
    public List<Project> projects = new();
    public Settlement capital;
    public GameState state = GameState.PLAYING;

    public int currentTurn = 0;

    private void Start()
    {
        if (Instance != null) return;
        Instance = this;
        // Start by placing initial embark randomly
        int x = Random.Range(0, hexWorldManager.latitude),
            z = Random.Range(0, hexWorldManager.longitude);

        var startResources = new ResourceCollection { resources = new List<ResourcePile>(embarkResources) }.Clone();
        capital = FoundSettlement(hexWorldManager.tiles[x, z], startResources, 7, true);
        Debug.Log("Embarked at " + x + ", " + z);
        var controls = Camera.main != null ? Camera.main.GetComponent<PlayerControls>() : null;
        if (controls != null) controls.FocusOn(capital.tile.Surface, true);

        gameObject.AddComponent<GameUI>();
    }

    private Settlement FoundSettlement(HexTile tile, ResourceCollection resources, int population, bool isCapital = false)
    {
        Settlement settlement = tile.gameObject.AddComponent<Settlement>();
        tile.settlement = settlement;
        settlement.tile = tile;
        settlement.resources = resources;
        settlement.population = population;
        settlement.isCapital = isCapital;
        settlement.RefreshModels();
        settlement.Lost += OnLoseSettlement;
        settlements.Add(settlement);
        return settlement;
    }

    public void AdvanceTurn()
    {
        if (state != GameState.PLAYING) return;

        foreach (var settlement in settlements.ToArray())
        {
            settlement.AdvanceTurn(farmYield, millConversions, millRunsPerTier);
        }
        if (state != GameState.PLAYING) return;

        foreach (var group in activeSettlers.ToArray())
        {
            int result = group.AdvanceTurn();
            if (result == 0) continue;
            activeSettlers.Remove(group);
            if (result > 0)
            {
                SettlersArrive(group);
                group.mover.Arrive();
            }
            else Destroy(group.gameObject);
        }

        foreach (var project in projects.ToArray())
        {
            if (project.mover != null) project.mover.Step();
            if (--project.turnsLeft > 0) continue;
            projects.Remove(project);
            if (project.mover != null) project.mover.Arrive();
            project.onComplete();
        }

        currentTurn++;
        if (capital.tier >= winTier) state = GameState.WON;
    }

    private void SettlersArrive(SettlerGroup group)
    {
        var tile = group.destination;
        if (tile.settlement != null)
        {
            tile.settlement.resources.Add(group.resources);
            tile.settlement.population += group.settlers;
        }
        else
        {
            FoundSettlement(tile, group.resources, group.settlers);
        }
    }

    private void OnLoseSettlement(Settlement settlement)
    {
        settlements.Remove(settlement);
        if (settlement.isCapital)
        {
            state = GameState.LOST;
        }
        settlement.tile.settlement = null;
        Destroy(settlement);
    }

    public bool IsBusy(Settlement settlement, ProjectType type) =>
        projects.Exists(p => p.owner == settlement && p.type == type);

    private Project StartProject(Settlement owner, ProjectType type, string name, int turns, Action onComplete)
    {
        var project = new Project { owner = owner, type = type, name = name, turnsLeft = Mathf.Max(1, turns), onComplete = onComplete };
        projects.Add(project);
        return project;
    }

    // ---- City upgrade ----

    public ResourceCollection UpgradeCost(Settlement s)
    {
        int next = s.tier + 1;
        if (capitolBuilding != null && next < capitolBuilding.tierBuildResources.Length)
            return capitolBuilding.tierBuildResources[next];
        return new ResourceCollection { resources = new List<ResourcePile>() };
    }

    public int UpgradeTurns(Settlement s)
    {
        int next = s.tier + 1;
        if (capitolBuilding != null && next < capitolBuilding.tierBuildTimes.Length && capitolBuilding.tierBuildTimes[next] > 0)
            return capitolBuilding.tierBuildTimes[next];
        return next;
    }

    public bool CanUpgradeCity(Settlement s, out string why)
    {
        why = null;
        if (!s.isCapital) why = "Only the capital can be upgraded";
        else if (s.tier >= winTier) why = "Already at max tier";
        else if (IsBusy(s, ProjectType.CITY_UPGRADE)) why = "Upgrade in progress";
        else if (!s.resources.Has(UpgradeCost(s))) why = "Needs " + UpgradeCost(s);
        return why == null;
    }

    public void UpgradeCity(Settlement s)
    {
        if (!CanUpgradeCity(s, out _)) return;
        s.resources.Remove(UpgradeCost(s));
        int next = s.tier + 1;
        StartProject(s, ProjectType.CITY_UPGRADE, "Upgrade " + s.DisplayName + " to T" + next, UpgradeTurns(s),
            () => { if (s != null) { s.tier = next; s.RefreshModels(); } });
    }

    // ---- Settlers ----

    public int SettlerTurns(Settlement from, HexTile to) => HexWorldManager.Distance(from.tile, to) * from.tier;

    public bool CanSendSettlers(Settlement from, HexTile to, out string why) =>
        CanSendSettlers(from, to, null, out why);

    // supplies: optional caravan that follows the settlers and arrives a turn after them
    public bool CanSendSettlers(Settlement from, HexTile to, ResourceCollection supplies, out string why)
    {
        why = null;
        var total = settlerResourceCost.Clone();
        if (supplies != null) total.Add(supplies);
        if (to.settlement != null) why = "Tile is already settled";
        else if (from.population <= settlerCount) why = "Needs more than " + settlerCount + " population";
        else if (!from.resources.Has(total)) why = "Needs " + total;
        return why == null;
    }

    public void SendSettlers(Settlement from, HexTile to, ResourceCollection supplies)
    {
        if (!CanSendSettlers(from, to, supplies, out _)) return;
        from.resources.Remove(settlerResourceCost);
        from.population -= settlerCount;

        int turns = Mathf.Max(1, SettlerTurns(from, to));
        var route = hexWorldManager.FindPath(from.tile, to, false);
        var mover = RouteMover.Create(settlerPrefab, route, turns);
        mover.name = "Settlers " + from.tile + " -> " + to;

        var group = mover.gameObject.AddComponent<SettlerGroup>();
        group.origin = from.tile;
        group.destination = to;
        group.totalTurns = group.turnsLeft = turns;
        group.resources = settlerResourceCost.Clone();
        group.settlers = settlerCount;
        group.mover = mover;
        activeSettlers.Add(group);

        if (supplies == null || supplies.resources.Count == 0) return;
        from.resources.Remove(supplies);
        var caravan = StartProject(from, ProjectType.SHIPMENT, "Supplies " + from.tile + " -> " + to + ": " + supplies, turns + 1, () =>
        {
            if (to.settlement != null) to.settlement.resources.Add(supplies);
            else if (from != null) from.resources.Add(supplies);
        });
        caravan.mover = RouteMover.Create(caravanPrefab, route, turns + 1);
    }

    // ---- Roads ----

    public RoadTier NextRoadTier(Path path) => path.MinRoadTier() + 1;

    public bool RoadResource(Settlement from, RoadTier tier, int amount, out ResourceData resource)
    {
        var flag = (RoadResourceType)(1 << ((int)tier - 1));
        foreach (var pile in from.resources.resources)
        {
            if ((pile.resource.roadResourceType & flag) != 0 && pile.amount >= amount)
            {
                resource = pile.resource;
                return true;
            }
        }
        resource = null;
        return false;
    }

    public bool CanBuildRoad(Settlement a, Settlement b, out int turns, out string why)
    {
        why = null;
        turns = 0;
        var path = hexWorldManager.FindPath(a.tile, b.tile, false);
        if (a == b) why = "Pick a different settlement";
        else if (IsBusy(a, ProjectType.ROAD)) why = "Road already under construction";
        else if (path.MinRoadTier() >= RoadTier.MAX_TIER) why = "Road is already max tier";
        else
        {
            var tier = NextRoadTier(path);
            turns = path.Length * (int)tier;
            if (!RoadResource(a, tier, turns, out _)) why = "Needs " + turns + " of a T" + (int)tier + " road material";
        }
        return why == null;
    }

    public void BuildRoad(Settlement a, Settlement b)
    {
        if (!CanBuildRoad(a, b, out int turns, out _)) return;
        var path = hexWorldManager.FindPath(a.tile, b.tile, false);
        var tier = NextRoadTier(path);
        RoadResource(a, tier, turns, out var material);
        a.resources.Remove(new ResourceCollection { resources = new List<ResourcePile> { new() { resource = material, amount = turns } } });
        StartProject(a, ProjectType.ROAD, "T" + (int)tier + " road " + a.tile + " -> " + b.tile, turns, () =>
        {
            for (int i = 0; i < path.Length; i++)
            {
                var from = path.nodes[i];
                var to = path.nodes[i + 1];
                if (from.RoadTo(to) < tier) hexWorldManager.SetRoad(from, to, tier);
            }
        });
    }

    // ---- Farm ----

    public bool CanBuildFarm(Settlement s, out string why)
    {
        why = null;
        if (s.hasFarm || IsBusy(s, ProjectType.FARM)) why = "Already has a farm";
        else if (!s.CanHaveFarm) why = "No primary resource on this tile";
        else if (!s.resources.Has(farmCost)) why = "Needs " + farmCost;
        return why == null;
    }

    public void BuildFarm(Settlement s)
    {
        if (!CanBuildFarm(s, out _)) return;
        s.resources.Remove(farmCost);
        StartProject(s, ProjectType.FARM, "Farm at " + s.DisplayName, 1, () => { if (s != null) { s.hasFarm = true; s.RefreshModels(); } });
    }

    // ---- Mill ----

    public ResourceCollection MillCost(Settlement s) => millCostPerTier.Clone(s.millTier + 1);

    public int MillTurns(Settlement s) => s.millTier + 2;

    public bool CanBuildMill(Settlement s, out string why)
    {
        why = null;
        if (IsBusy(s, ProjectType.MILL)) why = "Mill under construction";
        else if (s.millTier >= maxMillTier) why = "Mill is already max tier";
        else if (!s.resources.Has(MillCost(s))) why = "Needs " + MillCost(s);
        return why == null;
    }

    public void BuildMill(Settlement s)
    {
        if (!CanBuildMill(s, out _)) return;
        s.resources.Remove(MillCost(s));
        int next = s.millTier + 1;
        StartProject(s, ProjectType.MILL, "T" + next + " mill at " + s.DisplayName, MillTurns(s), () => { if (s != null) { s.millTier = next; s.RefreshModels(); } });
    }

    // ---- Shipments ----

    public bool CanShip(Settlement from, Settlement to, out int turns, out string why)
    {
        why = null;
        turns = 0;
        var path = from == to ? null : hexWorldManager.FindPath(from.tile, to.tile);
        if (from == to) why = "Pick a different settlement";
        else if (path == null) why = "No road connects these settlements";
        else turns = path.ShippingTurns();
        return why == null;
    }

    public void Ship(Settlement from, Settlement to, ResourceCollection cargo)
    {
        if (!CanShip(from, to, out int turns, out _) || from.resources.Remove(cargo) < 0) return;
        var shipment = StartProject(from, ProjectType.SHIPMENT, "Shipment " + from.tile + " -> " + to.tile + ": " + cargo, turns, () =>
        {
            if (to != null) to.resources.Add(cargo);
        });
        shipment.mover = RouteMover.Create(caravanPrefab, hexWorldManager.FindPath(from.tile, to.tile), turns);
    }
}
