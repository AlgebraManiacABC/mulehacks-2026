using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BiomeData", menuName = "Scriptable Objects/BiomeData")]
public class BiomeData : ScriptableObject
{
    public string biomeName;
    // The top texture of the tile
    public Material material;
    // The climate this biome prefers; each tile gets the biome closest to its own climate
    [Range(0f, 1f)] public float elevation = 0.5f;
    [Range(0f, 1f)] public float moisture = 0.5f;
    // A set of resources which are possible to find in this biome,
    //  matched with the probability that it will appear.
    // The list of struct is for Inspector serialization
    [System.Serializable]
    public struct ResourceProbability
    {
        public ResourceData resource;
        public float probability;
    }
    
    public List<ResourceProbability> resourcesPossible;

    public ResourceData rollResource()
    {
        float r = Random.value;
        float p_sum = 0;
        foreach (var rp in resourcesPossible)
        {
            if (r > p_sum && r < (p_sum += rp.probability)) return rp.resource;
        }
        return null;
    }
}
