using System.Text.Json;
using NormaCase.Knowledge.Serialization;
using NormaCase.RuleEngine.Evaluation;
using NormaCase.Serialization;

// Usage: dotnet run -- <cases.json> <output-dir>
var cases = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(args[0]),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
Directory.CreateDirectory(args[1]);
var loader = new KnowledgePackLoader();
var evaluator = new RuleEvaluator();
foreach (var entry in cases)
{
    var pack = loader.LoadFromJson(File.ReadAllText(entry.Pack));
    var input = CaseInputJson.Deserialize(File.ReadAllText(entry.Case));
    var result = evaluator.Evaluate(pack, input.Facts, input.AssessmentDate, input.Evidence);
    File.WriteAllText(Path.Combine(args[1], entry.Name + ".json"), AssessmentJson.Serialize(result, "parity") + "\n");
}
Console.WriteLine($"{cases.Count} assessments written to {args[1]}");

record Entry(string Name, string Pack, string Case);
