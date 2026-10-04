using UnityEngine;

[CreateAssetMenu(fileName = "ResourceData", menuName = "Scriptable Objects/ResourceData")]
public class ResourceData : ScriptableObject
{
    public string resourceName;
    // Primary resources just need a farm on the tile to harvest.
    // Secondary resources require processing a primary resource.
    public bool isPrimary;
    // What types of roads this resource can be used in
    public RoadResourceType roadResourceType;
    // Whether this resource is used in buildings
    public bool isArchitectural;
    // Whether this resource can be consumed by civilians
    public bool isFood;
    // Whether this resource is a luxury (required by certain higher tier capitals)
    public bool isLuxury;
    // The GameObject to add onto the tile for this resource
    public GameObject display;
}
