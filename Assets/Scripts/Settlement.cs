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
    // What building is currently being constructed
    public List<Building> constructions = new List<Building>();
    // A timer for building construction
    public int turnTimer;
    // The completed buildings on this tile
    public List<Building> buildings = new List<Building>();
    // The tile this settlement exists on
    public HexTile tile;
    
    // Action to alert TurnManager of loss of settlement
    public event Action<Settlement> Lost;
    
    void SendSettlers(HexTile dest)
    {
        Path path = HexWorldManager.Instance.FindPath(tile, dest);
        if (path == null)
        {
            // Something bad...
            throw new InvalidOperationException("Attempted to form a path between two completely disconnected hexagons!");
        }
        SettlerGroup group = gameObject.AddComponent<SettlerGroup>();
        group.path = path;
        group.path_index = 0;
        int resourcesLeft = resources.Remove(TurnManager.Instance.settlerResourceCost);
        if (resourcesLeft < 0)
        {
            // Can't send settlers; surely we checked for this earlier?
            throw new InvalidOperationException("Attempted to send settlers with insufficient resources!");
        }
        group.resources = TurnManager.Instance.settlerResourceCost;
    }

    void AdvanceTurn()
    {
        // Increment resources according to farms on this tile
        // TODO: Add farms
        // Decrement food resources 1 per pop
        int hungry_population = population;
        foreach (ResourcePile pile in resources.resources)
        {
            if (pile.resource.isFood)
            {
                if (pile.amount > hungry_population)
                {
                    pile.amount -= hungry_population;
                    hungry_population = 0;
                }
                else
                {
                    hungry_population -= pile.amount;
                    pile.amount = 0;
                }
            }
            if (hungry_population == 0) break;
        }
        // If still hungry, lose all hungry_population
        population -= hungry_population;
        if (population == 0)
        {
            // Lose the settlement
            Lost?.Invoke(this);
            return;
        }
        // If high enough resources, increase population
        // TODO: Grow population
        // Decrement build timers
        if (turnTimer > 0)
        {
            turnTimer--;
        }
    }
}
