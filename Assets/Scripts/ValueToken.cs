// ValueToken.cs
using System.Text.RegularExpressions;

public enum TokenKind { StringLiteral, Number, Identifier, Invalid }

public static class ValueToken
{
    static readonly Regex intRx = new(@"^-?\d+$");
    static readonly Regex idRx = new(@"^[A-Za-z_][A-Za-z0-9_]*$");

    /// Returns (kind, displayLabel, rawText)
    public static (TokenKind kind, string label, string raw) Parse(string raw)
    {
        var s = (raw ?? "").Trim();
        if (s.Length == 0) return (TokenKind.Invalid, s, raw);
        if (s.StartsWith("\"") && s.EndsWith("\"") && s.Length >= 2)
        {
            var inner = s.Substring(1, s.Length - 2);
            return (TokenKind.StringLiteral, inner, raw);
        }
        if (intRx.IsMatch(s)) return (TokenKind.Number, s, raw);
        if (idRx.IsMatch(s)) return (TokenKind.Identifier, s, raw);
        return (TokenKind.Invalid, s, raw);
    }
}
