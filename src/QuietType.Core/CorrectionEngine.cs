namespace QuietType.Core;

public sealed class CorrectionEngine(Lexicon lexicon)
{
    private static readonly Dictionary<string, string> CommonTypos = new(StringComparer.Ordinal)
    {
        ["teh"]="the", ["hte"]="the", ["thier"]="their", ["recieve"]="receive", ["recieved"]="received",
        ["becuase"]="because", ["beacuse"]="because", ["becasue"]="because", ["definately"]="definitely",
        ["seperate"]="separate", ["seperately"]="separately", ["occured"]="occurred", ["untill"]="until",
        ["wierd"]="weird", ["wierdest"]="weirdest", ["asnwer"]="answer", ["answre"]="answer",
        ["yhou"]="you", ["completelyt"]="completely", ["whgatever"]="whatever", ["intrusiuve"]="intrusive",
        ["tytpingh"]="typing", ["tyipng"]="typing", ["typping"]="typing", ["typoes"]="typos",
        ["taht"]="that", ["thta"]="that", ["wiht"]="with", ["wihch"]="which", ["whcih"]="which",
        ["adn"]="and", ["nad"]="and", ["alot"]="a lot", ["doesnt"]="doesn't", ["didnt"]="didn't",
        ["isnt"]="isn't", ["wasnt"]="wasn't", ["wouldnt"]="wouldn't", ["couldnt"]="couldn't",
        ["shouldnt"]="shouldn't", ["havent"]="haven't", ["hasnt"]="hasn't", ["youre"]="you're",
        ["immediatly"]="immediately", ["privte"]="private", ["progam"]="program", ["progarm"]="program",
        ["instalation"]="installation", ["instllation"]="installation", ["thnaks"]="thanks", ["pleaes"]="please"
    };
    private static readonly Dictionary<string, string[]> Confusions = new(StringComparer.Ordinal)
    {
        ["form"]=["form","from"], ["from"]=["from","form"],
        ["then"]=["then","than"], ["than"]=["than","then"],
        ["their"]=["their","there"], ["there"]=["there","their"],
        ["quiet"]=["quiet","quite"], ["quite"]=["quite","quiet"],
        ["lose"]=["lose","loose"], ["loose"]=["loose","lose"]
    };
    public bool NeedsContext(string before, Settings settings)
    {
        var target = TextRules.FindTarget(before);
        if (target is null || settings.PersonalWords.Contains(target.Word, StringComparer.OrdinalIgnoreCase)) return false;
        string word = target.Word.ToLowerInvariant();
        if (lexicon.Contains(word)) return settings.CorrectRealWords && Confusions.ContainsKey(word);
        return !char.IsUpper(target.Word[0]) && lexicon.Candidates(word).Count > 0;
    }
    public Correction? Suggest(string before, string after, Settings settings, ContextModel? model)
    {
        var target = TextRules.FindTarget(before);
        if (target is null) return null;
        var word = target.Word.ToLowerInvariant();
        if (settings.PersonalWords.Contains(word, StringComparer.OrdinalIgnoreCase)) return null;
        string? replacement = null;
        string reason = "Spelling";
        if (CommonTypos.TryGetValue(word, out var common)) replacement = common;
        else if (lexicon.Contains(word))
        {
            if (!settings.CorrectRealWords || model is null || !Confusions.TryGetValue(word, out var group)) return null;
            var scores = model.Score(before[..target.Start], target.Tail + after, group);
            var best = scores.OrderByDescending(x => x.Value.Logit).FirstOrDefault();
            if (best.Key is not null && best.Key != word && scores.TryGetValue(word, out var original) && best.Value.Logit - original.Logit >= 5 && best.Value.Probability >= 0.2)
            { replacement = best.Key; reason = "Context"; }
        }
        else
        {
            if (char.IsUpper(target.Word[0])) return null; // Unknown names are not spelling mistakes.
            var candidates = lexicon.Candidates(word);
            if (candidates.Count == 0) return null;
            var first = candidates[0];
            // Clear spelling mistakes need no neural inference.
            if (first.Distance == 1 && first.Frequency >= 1000000 &&
                (candidates.Count == 1 || candidates[1].Distance > 1 || first.Frequency >= candidates[1].Frequency * 30.0)) replacement = first.Word;
            else if (model is not null)
            {
                var scores = model.Score(before[..target.Start], target.Tail + after, candidates.Select(x => x.Word));
                var ranked = candidates.Where(x => scores.ContainsKey(x.Word))
                    .Select(x => (Candidate: x, Score: scores[x.Word].Logit - 1.5 * x.Distance + 0.12 * Math.Log(x.Frequency)))
                    .OrderByDescending(x => x.Score).ToList();
                if (ranked.Count > 0 && scores[ranked[0].Candidate.Word].Probability >= 0.00001 &&
                    (ranked.Count == 1 || ranked[0].Score - ranked[1].Score >= 1.25))
                { replacement = ranked[0].Candidate.Word; reason = "Spelling + context"; }
            }
        }
        return replacement is null || replacement == word ? null : new(target.Word, TextRules.PreserveCase(target.Word, replacement), target.Start, target.Tail, reason);
    }
}
