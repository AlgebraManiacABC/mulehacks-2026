using System;
using System.Collections.Generic;
using UnityEngine;

public class Settlement : MonoBehaviour
{
    [Serializable]
    public class Processor
    {
        public Building building;
        public int tier;
        public GameObject model;
        public int shownTier;
    }

    // Whether this settlement is the capital of a kingdom
    public bool isCapital;
    // The resources this settlement currently has
    public ResourceCollection resources;
    // The population of the settlement
    public int population;
    // Tier of the settlement (only the capital can be upgraded)
    public int tier = 1;
    // Whether this tile's resource is being harvested (by Producer)
    public bool hasProducer;
    // Buildings converting resources (gristmill, butchery...)
    public List<Processor> processors = new List<Processor>();
    // The tile this settlement exists on
    public HexTile tile;
    // Models shown on the tile
    public GameObject model, producerModel;
    // What each model currently shows, so unchanged ones aren't rebuilt
    private int modelKey = -1, producerKey = -1;

    public string DisplayName => (isCapital ? "Capital " : "Settlement ") + tile;

    // Action to alert TurnManager of loss of settlement
    public event Action<Settlement> Lost;

    // The building that can harvest this tile's resource, if any
    public Building Producer => TurnManager.Instance.ProducerFor(tile.resource);

    public int MaxPopulation => 10 * tier;

    public int ProcessorTier(Building building)
    {
        var p = processors.Find(x => x.building == building);
        return p != null ? p.tier : 0;
    }

    public void SetProcessorTier(Building building, int newTier)
    {
        var p = processors.Find(x => x.building == building);
        if (p == null) processors.Add(p = new Processor { building = building });
        p.tier = newTier;
        RefreshModels();
    }

    public string BuildingSummary()
    {
        var names = new List<string>();
        if (hasProducer && Producer != null) names.Add(Producer.DisplayName);
        foreach (var p in processors) names.Add(p.building.DisplayName + " T" + p.tier);
        return names.Count > 0 ? string.Join(", ", names) : "none";
    }

    public void RefreshModels()
    {
        var tm = TurnManager.Instance;
        Replace(ref model, ref modelKey, isCapital ? tier : 0,
            isCapital ? tm.capitolBuilding.Model(tier) : tm.embarkBuilding.Model(0), HexTile.CenterSlot);
        Replace(ref producerModel, ref producerKey, hasProducer ? 1 : 0,
            hasProducer && Producer != null ? Producer.Model(0) : null, HexTile.ProducerSlot);
        for (int i = 0; i < processors.Count; i++)
        {
            var p = processors[i];
            var slot = HexTile.ProcessorSlots[i % HexTile.ProcessorSlots.Length];
            if (Replace(ref p.model, ref p.shownTier, p.tier, p.building.Model(0), slot) && p.model != null)
                p.model.transform.localScale *= 1f + 0.15f * (p.tier - 1);
        }
    }

    private bool Replace(ref GameObject current, ref int currentKey, int key, GameObject prefab, Vector3 slot)
    {
        if (currentKey == key) return false;
        currentKey = key;
        if (current != null)
        {
            var anim = current.GetComponent<BuildAnimation>();
            if (anim != null) anim.Sink();
            else Destroy(current);
        }
        current = tile.Place(prefab, slot, true);
        return true;
    }

    private void OnDestroy()
    {
        if (model != null) Destroy(model);
        if (producerModel != null) Destroy(producerModel);
        foreach (var p in processors)
            if (p.model != null) Destroy(p.model);
    }

    public void AdvanceTurn(int runsPerTier)
    {
        if (hasProducer && Producer != null)
        {
            resources.Add(Producer.Generation(1));
        }

        foreach (var p in processors)
        {
            foreach (var conversion in p.building.tierResourceConversion)
            {
                for (int run = 0; run < p.tier * runsPerTier && resources.Has(conversion.input); run++)
                {
                    if (conversion.expendResource) resources.Remove(conversion.input);
                    resources.Add(conversion.output);
                }
            }
        }

        // Decrement food resources 1 per pop
        int hungry_population = population;
        foreach (ResourcePile pile in resources.resources)
        {
            if (hungry_population == 0) break;
            if (!pile.resource.isFood) continue;
            int eaten = Mathf.Min(pile.amount, hungry_population);
            pile.amount -= eaten;
            hungry_population -= eaten;
        }
        resources.resources.RemoveAll(p => p.amount <= 0);
        // Half of the hungry (rounded up) starve each turn
        population -= (hungry_population + 1) / 2;
        if (population <= 0)
        {
            population = 0;
            Lost?.Invoke(this);
            return;
        }

        // Grow if there is at least a turn's worth of food left over
        int food = FoodStock();
        if (hungry_population == 0 && food >= population && population < MaxPopulation)
        {
            population++;
        }
    }

    public int FoodStock()
    {
        int food = 0;
        foreach (var pile in resources.resources)
            if (pile.resource.isFood) food += pile.amount;
        return food;
    }
}
