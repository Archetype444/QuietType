using System.Globalization;
using System.Text.RegularExpressions;
namespace QuietType.Core;

public sealed record Candidate(string Word, long Frequency, int Distance);
public sealed record WordTarget(string Word, int Start, string Tail);
public sealed record Correction(string Original, string Replacement, int Start, string Tail, string Reason);

public sealed class Lexicon
{
    private readonly Dictionary<string, long> words = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> index = new(StringComparer.Ordinal);
    public int Count => words.Count;
    public Lexicon(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !long.TryParse(parts[1], CultureInfo.InvariantCulture, out var count)) continue;
            var word = parts[0];
            if (word.Length < 2 || word.Length > 28 || !word.All(char.IsAsciiLetter)) continue;
            words[word] = count;
        }
        foreach (var word in words.Keys)
            foreach (var key in Deletes(Prefix(word), 1))
            {
                if (!index.TryGetValue(key, out var list)) index[key] = list = [];
                list.Add(word);
            }
    }
    public bool Contains(string word) => words.ContainsKey(word);
    public long Frequency(string word) => words.GetValueOrDefault(word);
    private static string Prefix(string word) => word[..Math.Min(word.Length, 7)];
    private static HashSet<string> Deletes(string word, int depth)
    {
        var found = new HashSet<string>(StringComparer.Ordinal) { word };
        var frontier = new List<string> { word };
        for (int d = 0; d < depth; d++)
        {
            var next = new List<string>();
            foreach (var s in frontier)
                for (int i = 0; i < s.Length; i++)
                {
                    var v = s.Remove(i, 1);
                    if (found.Add(v)) next.Add(v);
                }
            frontier = next;
        }
        return found;
    }
    public List<Candidate> Candidates(string word)
    {
        var pool = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in Deletes(Prefix(word), 2))
        {
            if (index.TryGetValue(key, out var list)) pool.UnionWith(list);
        }
        return pool.Where(x => Math.Abs(x.Length - word.Length) <= 2)
            .Select(x => new Candidate(x, Frequency(x), Distance(word, x)))
            .Where(x => x.Distance <= 2 && x.Frequency >= 10000)
            .OrderBy(x => x.Distance).ThenByDescending(x => x.Frequency).Take(12).ToList();
    }
    // Optimal string alignment distance; adjacent transpositions count as one typo.
    public static int Distance(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 2) return 3;
        Span<int> matrix = stackalloc int[(a.Length + 1) * (b.Length + 1)];
        int width = b.Length + 1;
        for (int i = 0; i <= a.Length; i++) matrix[i * width] = i;
        for (int j = 0; j <= b.Length; j++) matrix[j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
            {
                int value = Math.Min(matrix[(i - 1) * width + j] + 1,
                    Math.Min(matrix[i * width + j - 1] + 1, matrix[(i - 1) * width + j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1)));
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1]) value = Math.Min(value, matrix[(i - 2) * width + j - 2] + 1);
                matrix[i * width + j] = value;
            }
        return matrix[a.Length * width + b.Length];
    }
}

public static partial class TextRules
{
    [GeneratedRegex(@"(?<![\w@/\\:.'’\-])[A-Za-z]{3,24}(?=[ ,.!?;:)]*$)", RegexOptions.CultureInvariant)]
    private static partial Regex LastWord();
    public static WordTarget? FindTarget(string before)
    {
        if (before.Length == 0 || !" ,.!?;:)".Contains(before[^1])) return null;
        var match = LastWord().Match(before);
        if (!match.Success) return null;
        var word = match.Value;
        if (word.All(char.IsUpper) || word.Skip(1).Any(char.IsUpper)) return null;
        var tail = before[(match.Index + match.Length)..];
        if (tail.Length > 8) return null;
        // Don't rewrite addresses, paths, handles, quoted code or identifiers.
        var start = before.LastIndexOfAny([' ', '\t', '\n', '\r'], Math.Max(0, match.Index - 1));
        var fragment = before[(start + 1)..];
        if (fragment.IndexOfAny(['@', '/', '\\', '_', '=', '`', '{', '}', '<', '>']) >= 0) return null;
        return new(word, match.Index, tail);
    }
    public static string BoundedTail(string value, int maximum)
    {
        int start = Math.Max(0, value.Length - maximum);
        if (start > 0 && char.IsLowSurrogate(value[start]) && char.IsHighSurrogate(value[start - 1])) start++;
        return value[start..];
    }
    public static string PreserveCase(string original, string replacement) => char.IsUpper(original[0]) ? char.ToUpperInvariant(replacement[0]) + replacement[1..] : replacement;
    public static bool CanReplace(string before, string original, string tail) =>
        original.Length is >= 1 and <= 24 && tail.Length <= 8 && original.All(char.IsAsciiLetter) &&
        tail.All(c => " ,.!?;:)".Contains(c)) && before.EndsWith(original + tail, StringComparison.Ordinal);
}
