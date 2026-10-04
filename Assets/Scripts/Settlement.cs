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
    // The model shown on the tile
    public GameObject model;

    public string DisplayName => (isCapital ? "Capital " : "Settlement ") + tile;

    // Action to alert TurnManager of loss of settlement
    public event Action<Settlement> Lost;

    public bool CanHaveFarm => tile.resource != null && tile.resource.isPrimary;

    public int MaxPopulation => 10 * tier;

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
