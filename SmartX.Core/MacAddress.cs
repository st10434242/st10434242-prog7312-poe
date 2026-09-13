namespace SmartX.Core;

// Parsing once into a type means nothing downstream has to re-check the format
public readonly record struct MacAddress
{
    private const int HexDigits = 12;

    private readonly string? _value;

    private MacAddress(string value) => _value = value;

    public bool IsEmpty => string.IsNullOrEmpty(_value);

    // Accepts AA:BB:CC:DD:EE:FF, AA-BB-CC-DD-EE-FF or AABBCCDDEEFF
    public static bool TryParse(string? text, out MacAddress mac)
    {
        mac = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string digits = text.Trim().Replace(":", "").Replace("-", "");

        if (digits.Length != HexDigits)
        {
            return false;
        }

        foreach (char c in digits)
        {
            if (!Uri.IsHexDigit(c))
            {
                return false;
            }
        }

        // Stored in one canonical form so equality does not care how it was typed
        string[] octets = new string[6];
        for (int i = 0; i < octets.Length; i++)
        {
            octets[i] = digits.Substring(i * 2, 2).ToUpperInvariant();
        }

        mac = new MacAddress(string.Join(':', octets));
        return true;
    }

    public static MacAddress Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return TryParse(text, out MacAddress mac)
            ? mac
            : throw new FormatException($"'{text}' is not a valid MAC address.");
    }

    public override string ToString() => _value ?? string.Empty;
}
