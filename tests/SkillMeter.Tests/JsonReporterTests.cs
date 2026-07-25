using System.Text.Json;
using SkillMeter.Model;
using SkillMeter.Output;
using Xunit;

namespace SkillMeter.Tests;

/// <summary>
/// The --json envelope is a published contract that consumers pin against. These
/// assert the wire format by key name, deliberately going through the serialized
/// text rather than the DTOs, so renaming a JsonPropertyName fails here.
/// </summary>
public sealed class JsonReporterTests
{
    private static JsonElement Render(BudgetReport report)
        => JsonDocument.Parse(JsonReporter.Render(report, "1.2.3")).RootElement;

    private static BudgetReport Sample() => Fixtures.Report(
        2_000,
        Fixtures.Skill("cheap", metadataTokens: 10, bodyTokens: 100, bodyLines: 5),
        Fixtures.Skill("dear", metadataTokens: 300, bodyTokens: 9_000, bodyLines: 600, resourceFiles: 2, resourceTokens: 50),
        Fixtures.Skill("middling", metadataTokens: 90));

    [Fact]
    public void SchemaVersionIsOne()
    {
        Assert.Equal(1, Render(Sample()).GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public void EveryDocumentedTopLevelKeyIsPresent()
    {
        var root = Render(Sample());

        foreach (var key in new[] { "schemaVersion", "tool", "toolVersion", "tokenizer", "budget", "totals", "skills" })
            Assert.True(root.TryGetProperty(key, out _), $"missing top-level key '{key}'");

        Assert.Equal("skillmeter", root.GetProperty("tool").GetString());
        Assert.Equal("1.2.3", root.GetProperty("toolVersion").GetString());
        Assert.Equal("o200k_base", root.GetProperty("tokenizer").GetString());
    }

    [Fact]
    public void EveryDocumentedBudgetAndTotalsKeyIsPresent()
    {
        var root = Render(Sample());
        var budget = root.GetProperty("budget");
        var totals = root.GetProperty("totals");

        foreach (var key in new[]
                 {
                     "contextWindow", "fraction", "budgetTokens", "maxDescChars", "overBudget",
                     "overageTokens", "budgetMultiple", "skillsThatFit", "skillsGoingDark", "fitIsUpperBound",
                 })
            Assert.True(budget.TryGetProperty(key, out _), $"missing budget key '{key}'");

        foreach (var key in new[]
                 {
                     "skillCount", "listedSkillCount", "notListedSkillCount", "metadataTokens",
                     "bodyTokens", "resourceTokens", "resourceFiles", "percentOfWindow",
                 })
            Assert.True(totals.TryGetProperty(key, out _), $"missing totals key '{key}'");
    }

    [Fact]
    public void EveryDocumentedSkillKeyIsPresent()
    {
        var skill = Render(Sample()).GetProperty("skills")[0];

        foreach (var key in new[]
                 {
                     "name", "path", "agent", "scope", "metadataTokens", "bodyTokens", "bodyLines",
                     "resourceTokens", "resourceFiles", "hasFrontmatter", "nameMismatch",
                     "listingTruncated", "exceedsBodyGuidance", "countsTowardListing",
                 })
            Assert.True(skill.TryGetProperty(key, out _), $"missing skill key '{key}'");
    }

    [Fact]
    public void SkillsAreSortedByMetadataTokensDescending()
    {
        var tokens = Render(Sample()).GetProperty("skills")
            .EnumerateArray().Select(s => s.GetProperty("metadataTokens").GetInt32()).ToList();

        Assert.Equal([300, 90, 10], tokens);
    }

    [Fact]
    public void BudgetMultipleRoundsToThreePlaces()
    {
        // 1000 / 3000 = 0.3333... must serialize as 0.333, not the full double.
        var root = Render(Fixtures.ReportWithMetadata(3_000, 1_000));

        Assert.Equal(0.333, root.GetProperty("budget").GetProperty("budgetMultiple").GetDouble());
    }

    [Fact]
    public void EmptyCorpusStillEmitsAValidEnvelope()
    {
        var root = Render(Fixtures.Report(2_000));

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Empty(root.GetProperty("skills").EnumerateArray());
        Assert.Equal(0, root.GetProperty("totals").GetProperty("skillCount").GetInt32());
        Assert.Equal(0, root.GetProperty("totals").GetProperty("metadataTokens").GetInt32());
        Assert.False(root.GetProperty("budget").GetProperty("overBudget").GetBoolean());
        // No skills means no division blowing up into NaN, which JSON cannot encode.
        Assert.Equal(0, root.GetProperty("budget").GetProperty("budgetMultiple").GetDouble());
    }

    [Fact]
    public void DisableModelInvocationSkillsAreCountedSeparately()
    {
        var root = Render(Fixtures.Report(
            2_000,
            Fixtures.Skill("listed", metadataTokens: 50),
            Fixtures.Skill("hidden", metadataTokens: 0, countsTowardListing: false)));

        var totals = root.GetProperty("totals");
        Assert.Equal(2, totals.GetProperty("skillCount").GetInt32());
        Assert.Equal(1, totals.GetProperty("listedSkillCount").GetInt32());
        Assert.Equal(1, totals.GetProperty("notListedSkillCount").GetInt32());
    }

    [Fact]
    public void OverBudgetIsReportedWithOverageAndMultiple()
    {
        var root = Render(Fixtures.ReportWithMetadata(2_000, 5_000));
        var budget = root.GetProperty("budget");

        Assert.True(budget.GetProperty("overBudget").GetBoolean());
        Assert.Equal(3_000, budget.GetProperty("overageTokens").GetInt32());
        Assert.Equal(2.5, budget.GetProperty("budgetMultiple").GetDouble());
    }

    [Fact]
    public void RootsEnvelopeCarriesItsOwnSchema()
    {
        var json = JsonReporter.RenderRoots(
            [new SkillRoot("/nowhere/at/all", AgentKind.Cursor, "user")], "1.2.3");
        var root = JsonDocument.Parse(json).RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("skillmeter", root.GetProperty("tool").GetString());

        var first = root.GetProperty("roots")[0];
        Assert.Equal("/nowhere/at/all", first.GetProperty("path").GetString());
        Assert.Equal("Cursor", first.GetProperty("agent").GetString());
        Assert.Equal("user", first.GetProperty("scope").GetString());
        Assert.False(first.GetProperty("exists").GetBoolean());
    }

    [Fact]
    public void OutputParsesAsJsonForEveryShape()
    {
        // Guards the AOT source-generated serializer: a context misconfiguration
        // shows up as malformed or empty output rather than an exception.
        foreach (var report in new[]
                 {
                     Fixtures.Report(2_000),
                     Sample(),
                     Fixtures.ReportWithMetadata(2_000, 100_000),
                 })
        {
            var text = JsonReporter.Render(report, "1.2.3");
            Assert.False(string.IsNullOrWhiteSpace(text));
            _ = JsonDocument.Parse(text);
        }
    }
}
