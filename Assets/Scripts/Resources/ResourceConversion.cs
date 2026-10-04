using UnityEngine;

[CreateAssetMenu(fileName = "ResourceConversion", menuName = "Scriptable Objects/ResourceConversion")]
public class ResourceConversion : ScriptableObject
{
    public ResourceCollection input;
    public ResourceCollection output;
    public bool expendResource;
}
