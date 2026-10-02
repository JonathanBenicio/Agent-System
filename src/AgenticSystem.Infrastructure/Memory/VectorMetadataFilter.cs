namespace AgenticSystem.Infrastructure.Memory;

internal static class VectorMetadataFilter
{
    internal static string[] ParseRoomIds(string value) => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.Ordinal).ToArray();

    internal static bool Matches(IReadOnlyDictionary<string, string> metadata, IReadOnlyDictionary<string, string> filters)
    {
        foreach (var (key, value) in filters)
        {
            if (key == "room_ids")
            {
                var hasRoom = metadata.TryGetValue("room_id", out var roomId) || metadata.TryGetValue("roomId", out roomId);
                if (!hasRoom || !ParseRoomIds(value).Contains(roomId, StringComparer.Ordinal))
                    return false;
            }
            else if (!metadata.TryGetValue(key, out var actual) || !string.Equals(actual, value, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }
}
