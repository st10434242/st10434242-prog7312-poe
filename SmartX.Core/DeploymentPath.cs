using System.Diagnostics.CodeAnalysis;

namespace SmartX.Core;

// Where a node sits in the hierarchy, e.g. FacilityA/Zone1/SubZoneB
public readonly struct DeploymentPath : IEquatable<DeploymentPath>
{
    public const char Separator = '/';

    // Rejecting deep paths here means the recursive tree walk never gets handed one
    public const int MaxDepth = 8;
    public const int MaxSegmentLength = 64;

    private readonly string? _canonical;

    private DeploymentPath(string canonical) => _canonical = canonical;

    public bool IsEmpty => string.IsNullOrEmpty(_canonical);

    public string Value => _canonical ?? string.Empty;

    public string[] Segments => IsEmpty ? [] : _canonical!.Split(Separator);

    public int Depth => Segments.Length;

    public string LeafName => IsEmpty ? string.Empty : Segments[^1];

    public static bool TryParse([NotNullWhen(true)] string? text, out DeploymentPath path)
    {
        path = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] segments = text.Split(Separator, StringSplitOptions.TrimEntries);

        if (segments.Length == 0 || segments.Length > MaxDepth)
        {
            return false;
        }

        foreach (string segment in segments)
        {
            if (segment.Length is 0 or > MaxSegmentLength)
            {
                return false;
            }

            // Letters, digits, hyphens and underscores only
            foreach (char c in segment)
            {
                if (!char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_'))
                {
                    return false;
                }
            }
        }

        path = new DeploymentPath(string.Join(Separator, segments));
        return true;
    }

    public static DeploymentPath Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return TryParse(text, out DeploymentPath path)
            ? path
            : throw new FormatException($"'{text}' is not a valid deployment path.");
    }

    public override string ToString() => Value;

    // Case insensitive so a zone typed as "zone1" still matches "Zone1"
    public bool Equals(DeploymentPath other) =>
        string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => obj is DeploymentPath other && Equals(other);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public static bool operator ==(DeploymentPath left, DeploymentPath right) => left.Equals(right);

    public static bool operator !=(DeploymentPath left, DeploymentPath right) => !left.Equals(right);
}
