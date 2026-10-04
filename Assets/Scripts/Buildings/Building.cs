using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Building", menuName = "Scriptable Objects/Building")]
public class Building : ScriptableObject
{
    // What kind of building this is
    public BuildingType BuildingType;
    // Name shown to the player
    public string displayName;
    
    // How long it takes to build each tier of this building
    [SerializeField]
    public int[] tierBuildTimes;
    
    // How many resources it takes to build each tier of this building
    [SerializeField]
    public ResourceCollection[] tierBuildResources;
    
    // What resources are generated from this building per tier
    [SerializeField]
    public ResourceCollection[] tierResourceGenerations;
    
    // What conversions this building can execute, each run (tier * runs per tier) times a turn
    [SerializeField]
    public ResourceConversion[] tierResourceConversion;
    
    // What models to use on this building's tile for each tier
    [SerializeField]
    public GameObject[] tierModels;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

    // Tiers are indexed from 1; index 0 is unused
    public int MaxTier => Mathf.Max(1, tierBuildResources.Length - 1);

    public ResourceCollection Cost(int tier) =>
        tier < tierBuildResources.Length ? tierBuildResources[tier] : new ResourceCollection { resources = new List<ResourcePile>() };

    public int BuildTime(int tier) =>
        tier < tierBuildTimes.Length && tierBuildTimes[tier] > 0 ? tierBuildTimes[tier] : tier + 1;

    public ResourceCollection Generation(int tier)
    {
        if (tierResourceGenerations.Length == 0) return new ResourceCollection { resources = new List<ResourcePile>() };
        return tierResourceGenerations[Mathf.Clamp(tier, 0, tierResourceGenerations.Length - 1)];
    }

    public GameObject Model(int tier) =>
        tierModels.Length == 0 ? null : tierModels[Mathf.Clamp(tier, 0, tierModels.Length - 1)];

    public bool Produces(ResourceData resource) => resource != null && Generation(1).Amount(resource) > 0;

    public bool Converts(ResourceData input, bool toFood)
    {
        foreach (var conversion in tierResourceConversion)
        {
            if (conversion.input.Amount(input) == 0) continue;
            if (!toFood) return true;
            foreach (var pile in conversion.output.resources)
                if (pile.resource.isFood) return true;
        }
        return false;
    }

    // e.g. "Wheat -> Flour"
    public string ConversionSummary()
    {
        var parts = new List<string>();
        foreach (var c in tierResourceConversion)
            parts.Add(Names(c.input) + " -> " + Names(c.output));
        return string.Join(", ", parts);
    }

    private static string Names(ResourceCollection collection)
    {
        var names = new List<string>();
        foreach (var pile in collection.resources) names.Add(pile.resource.resourceName);
        return string.Join("+", names);
    }
}
