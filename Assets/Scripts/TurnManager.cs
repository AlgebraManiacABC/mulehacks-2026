using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// SUPPLIES is a caravan following settlers; it returns home if they never settled
public enum ProjectType { CITY_UPGRADE, PRODUCER, PROCESSOR, ROAD, SHIPMENT, SUPPLIES }

public enum GameState { PLAYING, WON, LOST }

// Everything a project needs to finish lives in its fields, so it can be saved
public class Project
{
    public string name;
    public ProjectType type;
    public Building building;
    public Settlement owner;
    public HexTile ownerTile;
    public int turnsLeft, totalTurns;
    // New tier for upgrades and processors
    public int targetTier;
    // Road: tiles to build along; shipments: the travelled route
    public Path route;
    public RoadTier roadTier;
    public HexTile destination;
    public ResourceCollection cargo;
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
    // Harvesting (FARM type) and processing (MILL type) buildings
    public List<Building> buildings;
    public GameObject settlerPrefab;
    public GameObject caravanPrefab;
    // The HexWorldManager, for ease of communication
    [SerializeField]
    public HexWorldManager hexWorldManager;
    // The amount of resources it takes to send out a settler group
    public ResourceCollection settlerResourceCost;
    // How many people leave with a settler group
    public int settlerCount = 2;
    // How many times a processor runs each conversion per turn, per tier
    public int processorRunsPerTier = 5;
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
        if (SaveSystem.PendingLoad != null) Restore(SaveSystem.PendingLoad);
        else Embark();
        SaveSystem.PendingLoad = null;

        var controls = Camera.main != null ? Camera.main.GetComponent<PlayerControls>() : null;
        if (controls != null) controls.FocusOn(capital.tile.Surface, true);
        gameObject.AddComponent<GameUI>();
    }

    private void Embark()
    {
        // Embark on a random tile that can feed the capital, with its buildings ready
        var candidates = new List<HexTile>();
        foreach (var tile in hexWorldManager.tiles)
            if (CanFeed(tile.resource)) candidates.Add(tile);
        if (candidates.Count == 0)
            foreach (var tile in hexWorldManager.tiles) candidates.Add(tile);
        var embarkTile = candidates[Random.Range(0, candidates.Count)];

        var startResources = new ResourceCollection { resources = new List<ResourcePile>(embarkResources) }.Clone();
        capital = FoundSettlement(embarkTile, startResources, 7, true);
        if (capital.Producer != null) capital.hasProducer = true;
        var processor = embarkTile.resource != null && !embarkTile.resource.isFood ? FoodProcessorFor(embarkTile.resource) : null;
        if (processor != null) capital.SetProcessorTier(processor, 1);
        capital.RefreshModels();
        Debug.Log("Embarked at " + embarkTile);
    }

    // configure runs before the first model refresh, so restored settlements don't flash their T1 model
    private Settlement FoundSettlement(HexTile tile, ResourceCollection resources, int population, bool isCapital = false,
        System.Action<Settlement> configure = null)
    {
        Settlement settlement = tile.gameObject.AddComponent<Settlement>();
        tile.settlement = settlement;
        settlement.tile = tile;
        settlement.resources = resources;
        settlement.population = population;
        settlement.isCapital = isCapital;
        configure?.Invoke(settlement);
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
            settlement.AdvanceTurn(processorRunsPerTier);
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
            Complete(project);
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

    public bool IsBusy(Settlement settlement, ProjectType type, Building building = null) =>
        projects.Exists(p => p.owner == settlement && p.type == type && (building == null || p.building == building));

    public Building ProducerFor(ResourceData resource) =>
        buildings.Find(b => b.BuildingType == BuildingType.FARM && b.Produces(resource));

    public IEnumerable<Building> Processors => buildings.FindAll(b => b.BuildingType == BuildingType.MILL);

    public Building FoodProcessorFor(ResourceData resource) =>
        buildings.Find(b => b.BuildingType == BuildingType.MILL && b.Converts(resource, true));

    // Whether a settlement on a tile with this resource can make its own food
    public bool CanFeed(ResourceData resource) =>
        resource != null && ProducerFor(resource) != null && (resource.isFood || FoodProcessorFor(resource) != null);

    private Project StartProject(Settlement owner, ProjectType type, string name, int turns)
    {
        turns = Mathf.Max(1, turns);
        var project = new Project { owner = owner, ownerTile = owner.tile, type = type, name = name, turnsLeft = turns, totalTurns = turns };
        projects.Add(project);
        return project;
    }

    private void Complete(Project p)
    {
        var owner = p.owner;
        switch (p.type)
        {
            case ProjectType.CITY_UPGRADE:
                if (owner == null) return;
                owner.tier = p.targetTier;
                owner.RefreshModels();
                break;
            case ProjectType.PRODUCER:
                if (owner == null) return;
                owner.hasProducer = true;
                owner.RefreshModels();
                break;
            case ProjectType.PROCESSOR:
                if (owner != null) owner.SetProcessorTier(p.building, p.targetTier);
                break;
            case ProjectType.ROAD:
                for (int i = 0; i < p.route.Length; i++)
                {
                    var from = p.route.nodes[i];
                    var to = p.route.nodes[i + 1];
                    if (from.RoadTo(to) < p.roadTier) hexWorldManager.SetRoad(from, to, p.roadTier, i * 0.35f);
                }
                break;
            case ProjectType.SHIPMENT:
                if (p.destination.settlement != null) p.destination.settlement.resources.Add(p.cargo);
                break;
            case ProjectType.SUPPLIES:
                if (p.destination.settlement != null) p.destination.settlement.resources.Add(p.cargo);
                else if (p.ownerTile.settlement != null) p.ownerTile.settlement.resources.Add(p.cargo);
                break;
        }
    }

    // ---- City upgrade ----

    public ResourceCollection UpgradeCost(Settlement s) => capitolBuilding.Cost(s.tier + 1);

    public int UpgradeTurns(Settlement s)
    {
        int next = s.tier + 1;
        return next < capitolBuilding.tierBuildTimes.Length && capitolBuilding.tierBuildTimes[next] > 0
            ? capitolBuilding.tierBuildTimes[next] : next;
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
        StartProject(s, ProjectType.CITY_UPGRADE, "Upgrade " + s.DisplayName + " to T" + next, UpgradeTurns(s)).targetTier = next;
    }

    // ---- Settlers ----

    // Travel cost of the cheapest route (roads help), times the sending settlement's tier
    public int SettlerTurns(Settlement from, HexTile to) =>
        hexWorldManager.FindPath(from.tile, to, PathMode.TRAVEL).TravelTurns(from.tier);

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

        var route = hexWorldManager.FindPath(from.tile, to, PathMode.TRAVEL);
        int turns = route.TravelTurns(from.tier);
        SpawnSettlers(route, turns, turns, settlerCount, settlerResourceCost.Clone());

        if (supplies == null || supplies.resources.Count == 0) return;
        from.resources.Remove(supplies);
        var caravan = StartProject(from, ProjectType.SUPPLIES, "Supplies " + from.tile + " -> " + to + ": " + supplies, turns + 1);
        caravan.destination = to;
        caravan.cargo = supplies;
        caravan.route = route;
        caravan.mover = RouteMover.Create(caravanPrefab, route, caravan.totalTurns);
    }

    private SettlerGroup SpawnSettlers(Path route, int totalTurns, int turnsLeft, int count, ResourceCollection carried)
    {
        var mover = RouteMover.Create(settlerPrefab, route, totalTurns);
        mover.SetElapsed(totalTurns - turnsLeft);
        mover.name = "Settlers " + route.nodes[0] + " -> " + route.nodes[route.Length];

        var group = mover.gameObject.AddComponent<SettlerGroup>();
        group.origin = route.nodes[0];
        group.destination = route.nodes[route.Length];
        group.route = route;
        group.totalTurns = totalTurns;
        group.turnsLeft = turnsLeft;
        group.resources = carried;
        group.settlers = count;
        group.mover = mover;
        activeSettlers.Add(group);
        return group;
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

    // Prefers reusing existing roads; only the steps below the new tier are built
    public bool CanBuildRoad(Settlement a, Settlement b, out int turns, out string why)
    {
        why = null;
        turns = 0;
        var path = a == b ? null : hexWorldManager.FindPath(a.tile, b.tile, PathMode.PLAN_ROAD);
        if (a == b) why = "Pick a different settlement";
        else if (IsBusy(a, ProjectType.ROAD)) why = "Road already under construction";
        else if (path.MinRoadTier() >= RoadTier.MAX_TIER) why = "Already connected by a max tier road";
        else
        {
            var tier = NextRoadTier(path);
            turns = path.StepsBelow(tier) * (int)tier;
            if (!RoadResource(a, tier, turns, out _)) why = "Needs " + turns + " of a T" + (int)tier + " road material";
        }
        return why == null;
    }

    public void BuildRoad(Settlement a, Settlement b)
    {
        if (!CanBuildRoad(a, b, out int turns, out _)) return;
        var path = hexWorldManager.FindPath(a.tile, b.tile, PathMode.PLAN_ROAD);
        var tier = NextRoadTier(path);
        RoadResource(a, tier, turns, out var material);
        a.resources.Remove(new ResourceCollection { resources = new List<ResourcePile> { new() { resource = material, amount = turns } } });
        var project = StartProject(a, ProjectType.ROAD, "T" + (int)tier + " road " + a.tile + " -> " + b.tile, turns);
        project.route = path;
        project.roadTier = tier;
    }

    // ---- Producers (harvest the tile's resource) ----

    public bool CanBuildProducer(Settlement s, out string why)
    {
        why = null;
        var producer = s.Producer;
        if (producer == null) why = "Nothing to harvest on this tile";
        else if (s.hasProducer || IsBusy(s, ProjectType.PRODUCER)) why = "Already built";
        else if (!s.resources.Has(producer.Cost(1))) why = "Needs " + producer.Cost(1);
        return why == null;
    }

    public void BuildProducer(Settlement s)
    {
        if (!CanBuildProducer(s, out _)) return;
        var producer = s.Producer;
        s.resources.Remove(producer.Cost(1));
        StartProject(s, ProjectType.PRODUCER, producer.DisplayName + " at " + s.DisplayName, producer.BuildTime(1)).building = producer;
    }

    // ---- Processors (convert resources) ----

    public bool CanBuildProcessor(Settlement s, Building b, out string why)
    {
        why = null;
        int next = s.ProcessorTier(b) + 1;
        if (IsBusy(s, ProjectType.PROCESSOR, b)) why = "Under construction";
        else if (next > b.MaxTier) why = "Already max tier";
        else if (!s.resources.Has(b.Cost(next))) why = "Needs " + b.Cost(next);
        return why == null;
    }

    public void BuildProcessor(Settlement s, Building b)
    {
        if (!CanBuildProcessor(s, b, out _)) return;
        int next = s.ProcessorTier(b) + 1;
        s.resources.Remove(b.Cost(next));
        var project = StartProject(s, ProjectType.PROCESSOR, "T" + next + " " + b.DisplayName + " at " + s.DisplayName, b.BuildTime(next));
        project.building = b;
        project.targetTier = next;
    }

    // ---- Shipments ----

    public bool CanShip(Settlement from, Settlement to, out int turns, out string why)
    {
        why = null;
        turns = 0;
        var path = from == to ? null : hexWorldManager.FindPath(from.tile, to.tile, PathMode.ROADS_ONLY);
        if (from == to) why = "Pick a different settlement";
        else if (path == null) why = "No road connects these settlements";
        else turns = path.TravelTurns();
        return why == null;
    }

    public void Ship(Settlement from, Settlement to, ResourceCollection cargo)
    {
        if (!CanShip(from, to, out int turns, out _) || from.resources.Remove(cargo) < 0) return;
        var shipment = StartProject(from, ProjectType.SHIPMENT, "Shipment " + from.tile + " -> " + to.tile + ": " + cargo, turns);
        shipment.destination = to.tile;
        shipment.cargo = cargo;
        shipment.route = hexWorldManager.FindPath(from.tile, to.tile, PathMode.ROADS_ONLY);
        shipment.mover = RouteMover.Create(caravanPrefab, shipment.route, turns);
    }

    // ---- Saving ----

    public SaveData Capture()
    {
        var world = hexWorldManager;
        int lon = world.longitude;
        var data = new SaveData { seed = world.Seed, latitude = world.latitude, longitude = lon, currentTurn = currentTurn };

        foreach (var tile in world.tiles)
        {
            data.tiles.Add(new TileSave
            {
                x = tile.x, z = tile.z, biome = tile.biome.name,
                resource = tile.resource != null ? tile.resource.name : null,
                height = tile.transform.position.y
            });
            for (int i = 0; i < 6; i++)
            {
                var other = tile.connections[i];
                // Each road once, from the tile that comes first
                if (other == null || tile.roads[i] == RoadTier.NO_ROAD || other.x * lon + other.z < tile.x * lon + tile.z) continue;
                data.roads.Add(new RoadSave { ax = tile.x, az = tile.z, bx = other.x, bz = other.z, tier = (int)tile.roads[i] });
            }
        }

        foreach (var s in settlements)
        {
            var save = new SettlementSave
            {
                x = s.tile.x, z = s.tile.z, tier = s.tier, population = s.population,
                isCapital = s.isCapital, hasProducer = s.hasProducer, resources = SaveSystem.Piles(s.resources)
            };
            foreach (var p in s.processors) save.processors.Add(new ProcessorSave { building = p.building.name, tier = p.tier });
            data.settlements.Add(save);
        }

        foreach (var g in activeSettlers)
        {
            data.settlers.Add(new SettlerSave
            {
                turnsLeft = g.turnsLeft, totalTurns = g.totalTurns, settlers = g.settlers,
                resources = SaveSystem.Piles(g.resources), route = SaveSystem.Route(g.route, lon)
            });
        }

        foreach (var p in projects)
        {
            data.projects.Add(new ProjectSave
            {
                name = p.name, type = p.type, building = p.building != null ? p.building.name : null,
                ownerX = p.ownerTile.x, ownerZ = p.ownerTile.z, turnsLeft = p.turnsLeft, totalTurns = p.totalTurns,
                targetTier = p.targetTier, roadTier = p.roadTier,
                destX = p.destination != null ? p.destination.x : -1, destZ = p.destination != null ? p.destination.z : -1,
                cargo = SaveSystem.Piles(p.cargo), route = SaveSystem.Route(p.route, lon)
            });
        }
        return data;
    }

    private void Restore(SaveData data)
    {
        var world = hexWorldManager;
        var tiles = world.tiles;
        currentTurn = data.currentTurn;

        foreach (var r in data.roads)
            world.SetRoad(tiles[r.ax, r.az], tiles[r.bx, r.bz], (RoadTier)r.tier);

        foreach (var s in data.settlements)
        {
            var settlement = FoundSettlement(tiles[s.x, s.z], SaveSystem.Collection(s.resources), s.population, s.isCapital, x =>
            {
                x.tier = s.tier;
                x.hasProducer = s.hasProducer;
                foreach (var p in s.processors)
                {
                    var building = buildings.Find(b => b.name == p.building);
                    if (building != null) x.processors.Add(new Settlement.Processor { building = building, tier = p.tier });
                }
            });
            if (s.isCapital) capital = settlement;
        }

        foreach (var g in data.settlers)
        {
            var route = SaveSystem.Route(g.route, tiles, data.longitude);
            if (route != null) SpawnSettlers(route, g.totalTurns, g.turnsLeft, g.settlers, SaveSystem.Collection(g.resources));
        }

        foreach (var p in data.projects)
        {
            var ownerTile = tiles[p.ownerX, p.ownerZ];
            var project = new Project
            {
                name = p.name, type = p.type, building = buildings.Find(b => b.name == p.building),
                owner = ownerTile.settlement, ownerTile = ownerTile, turnsLeft = p.turnsLeft, totalTurns = p.totalTurns,
                targetTier = p.targetTier, roadTier = p.roadTier,
                destination = p.destX >= 0 ? tiles[p.destX, p.destZ] : null,
                cargo = SaveSystem.Collection(p.cargo), route = SaveSystem.Route(p.route, tiles, data.longitude)
            };
            if ((p.type == ProjectType.SHIPMENT || p.type == ProjectType.SUPPLIES) && project.route != null)
            {
                project.mover = RouteMover.Create(caravanPrefab, project.route, project.totalTurns);
                project.mover.SetElapsed(project.totalTurns - project.turnsLeft);
            }
            projects.Add(project);
        }

        if (capital == null && settlements.Count > 0) capital = settlements[0];
        Debug.Log("Loaded save from turn " + currentTurn);
    }
}
