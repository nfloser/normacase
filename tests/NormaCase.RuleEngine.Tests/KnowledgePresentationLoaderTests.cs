using System.Text.Json;
using System.Text.Json.Nodes;
using NormaCase.Knowledge.Presentation;
using NormaCase.Knowledge.Serialization;
using Xunit;

namespace NormaCase.RuleEngine.Tests;

public sealed class KnowledgePresentationLoaderTests
{
    private readonly KnowledgePackLoader _packLoader = new();
    private readonly KnowledgePresentationLoader _presentationLoader = new();

    [Fact]
    public void Valid_German_presentation_is_loaded_against_the_exact_pack_contract()
    {
        var presentation = _presentationLoader.LoadFromJson(
            LoadPack("demo-e"),
            PresentationJson("demo-e"),
            "de-DE");

        Assert.Equal("synthetic.demo-e", presentation.PackId);
        Assert.Equal("de-DE", presentation.Locale);
        Assert.Equal("Demo E – Mehrere Ergebnisse", presentation.Name);
        Assert.Equal("Kennzahl", presentation.Fields["metric"].Label);
        Assert.Equal(
            "Extern ausstehend",
            presentation.Outputs["external_state"]
                .Choices["PENDING_EXTERNAL"]);
        Assert.Equal(2, presentation.Examples.Count);
    }

    [Theory]
    [InlineData("formatVersion", "2")]
    [InlineData("locale", "\"en-US\"")]
    [InlineData("packId", "\"synthetic.demo-a\"")]
    public void Version_locale_and_pack_identity_must_match(
        string property,
        string replacementJson)
    {
        var node = JsonNode.Parse(PresentationJson("demo-e"))!;
        node[property] = JsonNode.Parse(replacementJson);

        Assert.Throws<InvalidOperationException>(
            () => _presentationLoader.LoadFromJson(
                LoadPack("demo-e"),
                node.ToJsonString(),
                "de-DE"));
    }

    [Fact]
    public void Field_ids_must_match_the_pack_exactly()
    {
        var node = JsonNode.Parse(PresentationJson("demo-a"))!;
        node["fields"]!.AsObject().Remove("criterion_c");

        Assert.Throws<InvalidOperationException>(
            () => _presentationLoader.LoadFromJson(
                LoadPack("demo-a"),
                node.ToJsonString(),
                "de-DE"));
    }

    [Fact]
    public void Output_choices_must_match_the_pack_exactly()
    {
        var node = JsonNode.Parse(PresentationJson("demo-e"))!;
        node["outputs"]!["external_state"]!["choices"]!
            .AsObject().Remove("PENDING_EXTERNAL");

        Assert.Throws<InvalidOperationException>(
            () => _presentationLoader.LoadFromJson(
                LoadPack("demo-e"),
                node.ToJsonString(),
                "de-DE"));
    }

    [Theory]
    [InlineData("name")]
    [InlineData("description")]
    public void Pack_level_user_visible_text_must_not_be_blank(
        string property)
    {
        var node = JsonNode.Parse(PresentationJson("demo-a"))!;
        node[property] = " ";

        Assert.Throws<InvalidOperationException>(
            () => _presentationLoader.LoadFromJson(
                LoadPack("demo-a"),
                node.ToJsonString(),
                "de-DE"));
    }

    [Fact]
    public void Field_and_output_labels_must_not_be_blank()
    {
        var node = JsonNode.Parse(PresentationJson("demo-e"))!;
        node["fields"]!["metric"]!["label"] = " ";

        Assert.Throws<InvalidOperationException>(
            () => _presentationLoader.LoadFromJson(
                LoadPack("demo-e"),
                node.ToJsonString(),
                "de-DE"));

        node = JsonNode.Parse(PresentationJson("demo-e"))!;
        node["outputs"]!["external_state"]!["label"] = " ";

        Assert.Throws<InvalidOperationException>(
            () => _presentationLoader.LoadFromJson(
                LoadPack("demo-e"),
                node.ToJsonString(),
                "de-DE"));
    }

    [Fact]
    public void Example_metadata_requires_safe_unique_file_references()
    {
        var node = JsonNode.Parse(PresentationJson("demo-a"))!;
        node["examples"]![0]!["caseFile"] = "../secret.json";

        Assert.Throws<InvalidOperationException>(
            () => _presentationLoader.LoadFromJson(
                LoadPack("demo-a"),
                node.ToJsonString(),
                "de-DE"));

        node = JsonNode.Parse(PresentationJson("demo-a"))!;
        node["examples"]![1]!["id"] =
            node["examples"]![0]!["id"]!.GetValue<string>();

        Assert.Throws<InvalidOperationException>(
            () => _presentationLoader.LoadFromJson(
                LoadPack("demo-a"),
                node.ToJsonString(),
                "de-DE"));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("label")]
    [InlineData("caseFile")]
    public void Example_required_text_must_not_be_blank(
        string property)
    {
        var node = JsonNode.Parse(
            PresentationJson("demo-a"))!;
        node["examples"]![0]![property] = " ";

        Assert.Throws<InvalidOperationException>(
            () => _presentationLoader.LoadFromJson(
                LoadPack("demo-a"),
                node.ToJsonString(),
                "de-DE"));
    }

    [Fact]
    public void Output_choice_labels_must_not_be_blank()
    {
        var node = JsonNode.Parse(
            PresentationJson("demo-e"))!;
        node["outputs"]!["external_state"]!["choices"]![
            "PENDING_EXTERNAL"] = " ";

        Assert.Throws<InvalidOperationException>(
            () => _presentationLoader.LoadFromJson(
                LoadPack("demo-e"),
                node.ToJsonString(),
                "de-DE"));
    }

    [Fact]
    public void Null_collection_entries_are_rejected_as_json()
    {
        var node = JsonNode.Parse(
            PresentationJson("demo-a"))!;
        node["examples"]![0] = null;

        Assert.Throws<JsonException>(
            () => _presentationLoader.LoadFromJson(
                LoadPack("demo-a"),
                node.ToJsonString(),
                "de-DE"));
    }

    [Fact]
    public void Missing_required_json_property_fails_closed()
    {
        var node = JsonNode.Parse(
            PresentationJson("demo-a"))!;
        node.AsObject().Remove("description");

        Assert.Throws<JsonException>(
            () => _presentationLoader.LoadFromJson(
                LoadPack("demo-a"),
                node.ToJsonString(),
                "de-DE"));
    }

    [Fact]
    public void Unknown_and_duplicate_json_properties_fail_closed()
    {
        var json = PresentationJson("demo-a");
        var node = JsonNode.Parse(json)!;
        node["extra"] = true;

        Assert.Throws<JsonException>(
            () => _presentationLoader.LoadFromJson(
                LoadPack("demo-a"),
                node.ToJsonString(),
                "de-DE"));

        Assert.Throws<JsonException>(
            () => _presentationLoader.LoadFromJson(
                LoadPack("demo-a"),
                json.Replace(
                    "\"formatVersion\": 1",
                    "\"formatVersion\": 1, \"formatVersion\": 1",
                    StringComparison.Ordinal),
                "de-DE"));
    }

    [Fact]
    public void Loaded_collections_are_detached_from_transport_collections()
    {
        var presentation = _presentationLoader.LoadFromJson(
            LoadPack("demo-e"),
            PresentationJson("demo-e"),
            "de-DE");

        Assert.Throws<NotSupportedException>(
            () => ((IDictionary<string, KnowledgePresentationText>)
                presentation.Fields).Clear());
        Assert.Throws<NotSupportedException>(
            () => ((IDictionary<string, string>)
                presentation.Outputs["external_state"].Choices).Clear());
    }

    private NormaCase.Knowledge.Model.KnowledgePack LoadPack(
        string demo)
        => _packLoader.LoadFromFile(
            Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                demo + "-pack.json"));

    private static string PresentationJson(string demo)
        => File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                demo + "-presentation.de-DE.json"));
}
