
[System.Flags]
public enum RoadResourceType
{
    NOT_FOR_ROADS = 0,
    FOR_TIER_1_ROADS = 1 << 0,
    FOR_TIER_2_ROADS = 1 << 1,
    FOR_TIER_3_ROADS = 1 << 2,
    FOR_TIER_4_ROADS = 1 << 3
}
