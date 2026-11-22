using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
namespace QuietType.Core;

public sealed partial class WordPiece
{
    private readonly Dictionary<string, int> vocabulary;
    public WordPiece(string path) => vocabulary = File.ReadLines(path).Select((s, i) => (s, i)).ToDictionary(x => x.s, x => x.i, StringComparer.Ordinal);
    public int Id(string word) => vocabulary.GetValueOrDefault(word, 100);
    [GeneratedRegex(@"[\p{L}\p{N}]+|[^\s\p{L}\p{N}]", RegexOptions.CultureInvariant)]
    private static partial Regex BasicTokens();
    public List<long> Encode(string text)
    {
        var normalized = new StringBuilder();
        foreach (char c in text.ToLowerInvariant().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) normalized.Append(c);
        var ids = new List<long>();
        foreach (Match match in BasicTokens().Matches(normalized.ToString()))
        {
            var word = match.Value;
            if (word.Length > 100) { ids.Add(100); continue; }
            var pieces = new List<long>();
            int start = 0;
            while (start < word.Length)
            {
                int end = word.Length, id = 100;
                for (; end > start; end--)
                    if (vocabulary.TryGetValue((start == 0 ? "" : "##") + word[start..end], out id)) break;
                if (end == start) { pieces.Clear(); pieces.Add(100); break; }
                pieces.Add(id); start = end;
            }
            ids.AddRange(pieces);
        }
        return ids;
    }
    public (long[] Ids, int MaskIndex) Masked(string prefix, string suffix)
    {
        var left = Encode(prefix).TakeLast(62).ToArray();
        var right = Encode(suffix).Take(30).ToArray();
        return ([101, ..left, 103, ..right, 102], left.Length + 1);
    }
}

public sealed class ContextModel : IDisposable
{
    private readonly InferenceSession session;
    private readonly WordPiece tokenizer;
    public double LastInferenceMs { get; private set; }
    public ContextModel(string directory)
    {
        OrtEnv.Instance().DisableTelemetryEvents();
        tokenizer = new(Path.Combine(directory, "vocab.txt"));
        using var options = new SessionOptions
        {
            IntraOpNumThreads = 2, InterOpNumThreads = 1,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            EnableProfiling = false,
            LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_FATAL
        };
        options.AddSessionConfigEntry("session.intra_op.allow_spinning", "0");
        options.AddSessionConfigEntry("session.inter_op.allow_spinning", "0");
        session = new(Path.Combine(directory, "model.onnx"), options);
    }
    public Dictionary<string, (float Logit, double Probability)> Score(string prefix, string suffix, IEnumerable<string> candidates)
    {
        var (ids, position) = tokenizer.Masked(prefix, suffix);
        var shape = new[] { 1, ids.Length };
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(ids, shape)),
            NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(Enumerable.Repeat(1L, ids.Length).ToArray(), shape))
        };
        if (session.InputMetadata.ContainsKey("token_type_ids")) inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(new long[ids.Length], shape)));
        var clock = System.Diagnostics.Stopwatch.StartNew();
        using var outputs = session.Run(inputs);
        LastInferenceMs = clock.Elapsed.TotalMilliseconds;
        var logits = outputs.First(x => x.Name == "logits").AsTensor<float>();
        int vocabSize = logits.Dimensions[2];
        float maximum = float.NegativeInfinity;
        for (int i = 0; i < vocabSize; i++) maximum = Math.Max(maximum, logits[0, position, i]);
        double sum = 0;
        for (int i = 0; i < vocabSize; i++) sum += Math.Exp(logits[0, position, i] - maximum);
        var result = new Dictionary<string, (float, double)>(StringComparer.Ordinal);
        foreach (var candidate in candidates.Distinct(StringComparer.Ordinal))
        {
            int id = tokenizer.Id(candidate);
            if (id == 100) continue;
            float score = logits[0, position, id];
            result[candidate] = (score, Math.Exp(score - maximum) / sum);
        }
        return result;
    }
    public void Dispose() => session.Dispose();
}
