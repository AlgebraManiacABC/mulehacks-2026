using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/**
 * HexWorldManager
 * ---------------
 *  Is in charge of any loaded world. Manages world creation,
 *   loading, saving, editing. Is in scope whenever a world is present.
 */
public class HexWorldManager : MonoBehaviour
{
    [SerializeField]
    public GameObject hexPrefab;
    // Space to keep track of the current world
    public HexTile[,] tiles;
    public int latitude; // x width in hexes
    public int longitude; // z width in hexes

    [SerializeField]
    public List<BiomeData> biomes;
    
    private void Start()
    {
        // The scene was just loaded; just create a world for now
        Random.InitState(Time.frameCount);
        
        // Load prefab hex for each position
        for (int x = 0; x < latitude; x++)
        {
            for (int z = 0; z < longitude; z++)
            {
                GameObject curHexObj = Instantiate(hexPrefab, HexToWorld(x, z, 5f), Quaternion.identity);
                HexTile curHex = curHexObj.GetComponent<HexTile>();
                curHex.biome = RandomBiome();
                if (curHex.biome.material != null)
                {
                    curHex.GetComponentInChildren<MeshRenderer>().material = curHex.biome.material;
                }
                curHex.resource = curHex.biome.rollResource();
                if (curHex.resource != null && curHex.resource.display != null)
                {
                    Instantiate(curHex.resource.display, curHexObj.transform);
                }
            }
        }
    }
    
    private static Vector3 HexToWorld(int col, int row, float a)
    {
        float x = col * Mathf.Sqrt(3f) * a;
        float z = row * 2f * a + (col % 2 == 1 ? a : 0f);   // shift odd columns
        return new Vector3(x, 0f, z);
    }

    private BiomeData RandomBiome()
    {
        return biomes[Random.Range(0, biomes.Count)];
    }
}
