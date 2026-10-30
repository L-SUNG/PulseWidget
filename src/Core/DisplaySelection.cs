namespace PulseWidget.Core;

public static class DisplaySelection
{
    public static IReadOnlyList<string> Select(IEnumerable<string> devices, string? primary, string? preferred, bool all)
    {
        var available = devices.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (all || available.Length == 0) return available;
        var selected = available.FirstOrDefault(d => string.Equals(d, preferred, StringComparison.OrdinalIgnoreCase))
            ?? available.FirstOrDefault(d => string.Equals(d, primary, StringComparison.OrdinalIgnoreCase)) ?? available[0];
        return new[] { selected };
    }
}
