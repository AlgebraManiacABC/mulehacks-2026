using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ResourceCollection
{
    // It is assumed that each resource type only appears once
    [SerializeField] public List<ResourcePile> resources;

    /**
     * Simulate taking a specified amount of resources from stock,
     *  which is NOT modified.
     * Returns the amount which would remain in stock after this extraction.
     * "Overdraw" is indicated by a negative returned amount.
     */
    public static int operator -(ResourceCollection stock, ResourcePile take)
    {
        foreach (var stockpile in stock.resources)
        {
            if (stockpile.resource.Equals(take.resource))
            {
                return stockpile.amount - take.amount;
            }
        }
        return -take.amount;
    }
    
    /**
     * Simulate taking an entire collection of resources from stock,
     *  which is NOT modified.
     * Returns the smallest remaining amount across all resources taken:
     *  (+): success, stock remains of every resource taken
     *  (0): success, but at least one resource would be depleted
     *  (-): at least one resource would be overdrawn (by the returned amount, for the worst one)
     */
    public static int operator -(ResourceCollection stock, ResourceCollection take)
    {
        int worst = int.MaxValue;
        foreach (var pile in take.resources)
        {
            if (pile.amount == 0) continue;   // taking nothing can't deplete anything
            worst = Mathf.Min(worst, stock - pile);
        }
        return worst;
    }
    
    /**
     * Take an entire collection of resources from stock, modifying it.
     * All-or-nothing: if any resource would be overdrawn, stock is left unchanged.
     * Returns the same result as (this - take):
     *  (+): removed, stock remains of every resource taken
     *  (0): removed, but at least one resource was depleted
     *  (-): NOT removed, at least one resource would be overdrawn
     */
    public int Remove(ResourceCollection take)
    {
        int result = this - take;
        if (result < 0) return result;

        foreach (var pile in take.resources)
        {
            if (pile.amount == 0) continue;
            for (int i = 0; i < resources.Count; i++)
            {
                if (resources[i].resource != pile.resource) continue;
                var stockpile = resources[i];   // copy (ResourcePile is a struct)
                stockpile.amount -= pile.amount;
                if (stockpile.amount <= 0)
                    resources.RemoveAt(i);      // depleted: stop tracking it
                else
                    resources[i] = stockpile;   // write back
                break;
            }
        }
        return result;
    }

    public int Amount(ResourceData resource)
    {
        foreach (var pile in resources)
            if (pile.resource == resource) return pile.amount;
        return 0;
    }

    public bool Has(ResourceCollection take) => this - take >= 0;

    public void Add(ResourceData resource, int amount)
    {
        if (amount <= 0) return;
        resources ??= new List<ResourcePile>();
        foreach (var pile in resources)
        {
            if (pile.resource != resource) continue;
            pile.amount += amount;
            return;
        }
        resources.Add(new ResourcePile { resource = resource, amount = amount });
    }

    public void Add(ResourceCollection other)
    {
        foreach (var pile in other.resources) Add(pile.resource, pile.amount);
    }

    public ResourceCollection Clone(int multiplier = 1)
    {
        var copy = new ResourceCollection { resources = new List<ResourcePile>() };
        if (resources == null) return copy;
        foreach (var pile in resources) copy.Add(pile.resource, pile.amount * multiplier);
        return copy;
    }

    public override string ToString()
    {
        if (resources == null || resources.Count == 0) return "nothing";
        var parts = new List<string>();
        foreach (var pile in resources) parts.Add(pile.amount + " " + pile.resource.resourceName);
        return string.Join(", ", parts);
    }
}
