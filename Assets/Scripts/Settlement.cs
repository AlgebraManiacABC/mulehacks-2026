using System;
using System.Collections.Generic;
using UnityEngine;

public class Settlement : MonoBehaviour
{
    // Whether this settlement is the capital of a kingdom
    public bool isCapital;
    // The resources this settlement currently has
    public ResourceCollection resources;
    // The population of the settlement
    public int population;
    // Tier of the settlement (only the capital can be upgraded)
    public int tier = 1;
    // Whether a farm harvests this tile's primary resource
    public bool hasFarm;
    // Tier of this settlement's mill (0 = no mill)
    public int millTier;
    // The tile this settlement exists on
    public HexTile tile;
    // Models shown on the tile
    public GameObject model, farmModel, millModel;
    // What each model currently shows, so unchanged ones aren't rebuilt
    private int modelKey = -1, farmKey = -1, millKey = -1;

    public string DisplayName => (isCapital ? "Capital " : "Settlement ") + tile;

    // Action to alert TurnManager of loss of settlement
    public event Action<Settlement> Lost;

    public bool CanHaveFarm => tile.resource != null && tile.resource.isPrimary;

    public int MaxPopulation => 10 * tier;

    public void RefreshModels()
    {
        var tm = TurnManager.Instance;
        Replace(ref model, ref modelKey, isCapital ? tier : 0,
            isCapital ? ModelFor(tm.capitolBuilding, tier) : ModelFor(tm.embarkBuilding, 0), HexTile.CenterSlot);
        Replace(ref farmModel, ref farmKey, hasFarm ? 1 : 0, hasFarm ? ModelFor(tm.farmBuilding, 0) : null, HexTile.FarmSlot);
        bool newMill = Replace(ref millModel, ref millKey, millTier,
            millTier > 0 ? ModelFor(tm.millBuilding, millTier - 1) : null, HexTile.MillSlot);
        if (newMill && millModel != null) millModel.transform.localScale *= 1f + 0.15f * (millTier - 1);
    }

    private static GameObject ModelFor(Building building, int index)
    {
        if (building == null || building.tierModels.Length == 0) return null;
        return building.tierModels[Mathf.Clamp(index, 0, building.tierModels.Length - 1)];
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
        if (farmModel != null) Destroy(farmModel);
        if (millModel != null) Destroy(millModel);
    }

    public void AdvanceTurn(int farmYield, IList<ResourceConversion> millConversions, int millRunsPerTier)
    {
        if (hasFarm && CanHaveFarm)
        {
            resources.Add(tile.resource, farmYield);
        }

        if (millTier > 0)
        {
            foreach (var conversion in millConversions)
            {
                for (int run = 0; run < millTier * millRunsPerTier && resources.Has(conversion.input); run++)
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
        // If still hungry, lose all hungry_population
        population -= hungry_population;
        if (population <= 0)
        {
            population = 0;
            Lost?.Invoke(this);
            return;
        }

        // Grow if there is at least a turn's worth of food left over
        int food = 0;
        foreach (var pile in resources.resources)
            if (pile.resource.isFood) food += pile.amount;
        if (hungry_population == 0 && food >= population && population < MaxPopulation)
        {
            population++;
        }
    }
}
