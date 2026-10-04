using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

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
    // The HexWorldManager, for ease of communication
    [SerializeField]
    public HexWorldManager hexWorldManager;
    // The amount of resources it takes to send out a settler group
    public ResourceCollection settlerResourceCost;
    // A list of settlers who have ventured forth
    public List<SettlerGroup> activeSettlers;

    public int currentTurn = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        if (Instance != null) return;
        Instance = this;
        // Start by placing initial embark randomly
        int x = Random.Range(0, hexWorldManager.latitude),
            z = Random.Range(0, hexWorldManager.longitude);

        Settlement embark = hexWorldManager.tiles[x, z].AddComponent<Settlement>();
        hexWorldManager.tiles[x, z].settlement = embark;
        embark.tile = hexWorldManager.tiles[x, z];
        embark.isCapital = true;
        embark.resources = new ResourceCollection
        {
            resources = new List<ResourcePile>(embarkResources)
        };
        embark.population = 7;
        embark.buildings.Add(Instantiate(embarkBuilding, transform));
        embark.Lost += OnLoseSettlement;
        Debug.Log("Embarked at " + x + ", " + z);
    }

    private void AdvanceTurn()
    {
        foreach (var group in activeSettlers)
        {
            int result = group.AdvanceTurn();
            if (result != 0)
            {
                // Lost settlers or founded new village
                Destroy(group);
            }
        }
        currentTurn++;
    }

    private void OnLoseSettlement(Settlement settlement)
    {
        if (settlement.isCapital)
        {
            // Game over!
            // TODO: Implement game over
        }
        settlement.tile.settlement = null;
        Destroy(settlement);
    }
}
