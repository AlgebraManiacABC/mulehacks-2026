
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Building", menuName = "Scriptable Objects/Building")]
public class Building : ScriptableObject
{
    // What kind of building this is
    public BuildingType BuildingType;
    
    // How long it takes to build each tier of this building
    [SerializeField]
    public int[] tierBuildTimes;
    
    // How many resources it takes to build each tier of this building
    [SerializeField]
    public ResourceCollection[] tierBuildResources;
    
    // What resources are generated from this building per tier
    [SerializeField]
    public ResourceCollection[] tierResourceGenerations;
    
    // What conversions this building can execute (only one per turn?)
    [SerializeField]
    public ResourceConversion[] tierResourceConversion;
    
    // What models to use on this building's tile for each tier
    [SerializeField]
    public GameObject[] tierModels;
}
