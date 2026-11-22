using System.Diagnostics;
using QuietType.Core;

string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath(".");
int passed = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); passed++; }
void Equal<T>(T expected, T actual, string message) => Check(Equals(expected, actual), $"{message}: expected {expected}, got {actual}");
var clock = Stopwatch.StartNew();
var lexicon = new Lexicon(Path.Combine(root,"assets/dictionary/frequency.txt"));
Console.WriteLine($"Lexicon: {lexicon.Count} words, {clock.ElapsedMilliseconds} ms");
Check(lexicon.Count > 60000,"Dictionary was loaded");
Equal(1,Lexicon.Distance("asnwer","answer"),"Transposition");
Equal(2,Lexicon.Distance("tytpingh","typing"),"Two insertions");
Equal(1,Lexicon.Distance("speling","spelling"),"Missing letter");
foreach(var word in new[]{"user@example.com ","https://example.com ","C:\\private\\filess ","user_nmae ","camelCsae ","NASA ","`typoo ","typoo\n","don't ","word-partt "})
 Check(TextRules.FindTarget(word) is null,"Unsafe target: "+word);
Equal("speling",TextRules.FindTarget("Some speling, ")!.Word,"Punctuation");
Equal(", ",TextRules.FindTarget("Some speling, ")!.Tail,"Tail preservation");
Check(TextRules.CanReplace("word speling ","speling"," "),"Valid suffix");
Check(!TextRules.CanReplace("word speling later ","speling"," "),"Stale suffix refused");
Check(!TextRules.CanReplace("speling\n","speling","\n"),"Newline refused");
var settings = new Settings();
var engine = new CorrectionEngine(lexicon);
Equal("answer",engine.Suggest("Please asnwer ","",settings,null)?.Replacement,"Common typo");
Equal("The",engine.Suggest("Teh ","",settings,null)?.Replacement,"Capitalization");
Check(engine.Suggest("Please asnwer ","",settings with {PersonalWords=["asnwer"]},null) is null,"Personal dictionary");
Check(engine.Suggest("I came form ","London",settings,null) is null,"Real words protected by default");
Equal(150,(settings with {IdleDelayMs=0}).Normalize().IdleDelayMs,"Settings lower bound");
Equal("code",(settings with {ExcludedApps=[" Code.EXE "]}).Normalize().ExcludedApps[0],"Process normalization");
Check(lexicon.Candidates("speling").Any(c=>c.Word=="spelling"),"Candidate recall");
var tokenizer = new WordPiece(Path.Combine(root,"assets/model/vocab.txt"));
Equal(tokenizer.Id("hello"),(int)tokenizer.Encode("Héllo")[0],"Tokenizer accents");
var (ids,mask) = tokenizer.Masked(string.Join(" ",Enumerable.Repeat("hello",100)),string.Join(" ",Enumerable.Repeat("world",100)));
Check(ids.Length<=95 && ids[mask]==103 && ids[0]==101 && ids[^1]==102,"Bounded tokenizer and mask");
using(var model = new ContextModel(Path.Combine(root,"assets/model")))
{
 var score=model.Score("I drink a cup of "," every morning.",["coffee","car","window"]);
 Check(score["coffee"].Logit>score["car"].Logit,"Neural context inference");
 Console.WriteLine($"Neural inference: {model.LastInferenceMs:F1} ms (two CPU threads)");
 foreach(var (text,expected) in new[]{("Please check my speling ","spelling"),("Please fix this sentnce ","sentence"),("I like drinkng ","drinking")})
 {
  var w=TextRules.FindTarget(text)!;
  var correction=engine.Suggest(text,"",settings,model);
  Console.WriteLine($"{text} => {correction?.Replacement ?? "(kept)"}");
  if (correction is null) foreach(var c in lexicon.Candidates(TextRules.FindTarget(text)!.Word)) Console.WriteLine(c);
  Equal(expected,correction?.Replacement,"General spelling");
 }
 var correct=engine.Suggest("I came form ","London.",settings with{CorrectRealWords=true},model);
 Console.WriteLine($"Real-word example: {correct?.Replacement ?? "(conservatively kept)"}");
}
Console.WriteLine($"PASS: {passed} assertions; peak process memory {Process.GetCurrentProcess().PeakWorkingSet64 / 1024 / 1024} MiB");
