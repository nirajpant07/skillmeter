using System.Text.Json;
using System.Text.Json.Serialization;
using SkillMeter.Model;

namespace SkillMeter.Output;

// Stable, versioned wire format. Bump SchemaVersion on any breaking change so CI
// consumers can pin. Records are plain DTOs, kept separate from the domain model
// precisely so refactoring the model cannot silently break someone's pipeline.

public sealed record JsonEnvelope
{
    /// <summary>
    /// 2 as of the skipped-path reporting: <c>skipped[]</c> and
    /// <c>totals.skippedCount</c> were added, so a consumer that assumed every key
    /// it knew about was the whole envelope sees a new one.
    /// </summary>
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; } = 2;

    [JsonPropertyName("tool")] public string Tool { get; init; } = "skillmeter";
    [JsonPropertyName("toolVersion")] public required string ToolVersion { get; init; }
    [JsonPropertyName("tokenizer")] public required string Tokenizer { get; init; }
    [JsonPropertyName("budget")] public required JsonBudget Budget { get; init; }
    [JsonPropertyName("totals")] public required JsonTotals Totals { get; init; }
    [JsonPropertyName("skills")] public required IReadOnlyList<JsonSkill> Skills { get; init; }

    /// <summary>
    /// Paths the scan could not read. Non-empty means every figure above is a lower
    /// bound. Always present, so a consumer can check it unconditionally.
    /// </summary>
    [JsonPropertyName("skipped")] public required IReadOnlyList<JsonSkipped> Skipped { get; init; }
}

public sealed record JsonSkipped
{
    [JsonPropertyName("path")] public required string Path { get; init; }
    [JsonPropertyName("reason")] public required string Reason { get; init; }
}

public sealed record JsonBudget
{
    [JsonPropertyName("contextWindow")] public required int ContextWindow { get; init; }
    [JsonPropertyName("fraction")] public required double Fraction { get; init; }
    [JsonPropertyName("budgetTokens")] public required int BudgetTokens { get; init; }
    [JsonPropertyName("maxDescChars")] public required int MaxDescChars { get; init; }
    [JsonPropertyName("overBudget")] public required bool OverBudget { get; init; }
    [JsonPropertyName("overageTokens")] public required int OverageTokens { get; init; }
    [JsonPropertyName("budgetMultiple")] public required double BudgetMultiple { get; init; }
    [JsonPropertyName("skillsThatFit")] public required int SkillsThatFit { get; init; }
    [JsonPropertyName("skillsGoingDark")] public required int SkillsGoingDark { get; init; }
    [JsonPropertyName("fitIsUpperBound")] public bool FitIsUpperBound { get; init; } = true;
}

public sealed record JsonTotals
{
    [JsonPropertyName("skillCount")] public required int SkillCount { get; init; }
    [JsonPropertyName("listedSkillCount")] public required int ListedSkillCount { get; init; }
    [JsonPropertyName("notListedSkillCount")] public required int NotListedSkillCount { get; init; }
    [JsonPropertyName("metadataTokens")] public required int MetadataTokens { get; init; }
    [JsonPropertyName("bodyTokens")] public required int BodyTokens { get; init; }
    [JsonPropertyName("resourceTokens")] public required int ResourceTokens { get; init; }
    [JsonPropertyName("resourceFiles")] public required int ResourceFiles { get; init; }
    [JsonPropertyName("percentOfWindow")] public required double PercentOfWindow { get; init; }

    /// <summary>How many paths the scan could not read. 0 means the totals are complete.</summary>
    [JsonPropertyName("skippedCount")] public required int SkippedCount { get; init; }
}

public sealed record JsonSkill
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("path")] public required string Path { get; init; }
    [JsonPropertyName("agent")] public required string Agent { get; init; }
    [JsonPropertyName("scope")] public required string Scope { get; init; }
    [JsonPropertyName("metadataTokens")] public required int MetadataTokens { get; init; }
    [JsonPropertyName("bodyTokens")] public required int BodyTokens { get; init; }
    [JsonPropertyName("bodyLines")] public required int BodyLines { get; init; }
    [JsonPropertyName("resourceTokens")] public required int ResourceTokens { get; init; }
    [JsonPropertyName("resourceFiles")] public required int ResourceFiles { get; init; }
    [JsonPropertyName("hasFrontmatter")] public required bool HasFrontmatter { get; init; }
    [JsonPropertyName("nameMismatch")] public required bool NameMismatch { get; init; }
    [JsonPropertyName("listingTruncated")] public required bool ListingTruncated { get; init; }
    [JsonPropertyName("exceedsBodyGuidance")] public required bool ExceedsBodyGuidance { get; init; }
    [JsonPropertyName("countsTowardListing")] public required bool CountsTowardListing { get; init; }
}

public sealed record JsonRootsEnvelope
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; } = 1;
    [JsonPropertyName("tool")] public string Tool { get; init; } = "skillmeter";
    [JsonPropertyName("toolVersion")] public required string ToolVersion { get; init; }
    [JsonPropertyName("roots")] public required IReadOnlyList<JsonRoot> Roots { get; init; }
}

public sealed record JsonRoot
{
    [JsonPropertyName("path")] public required string Path { get; init; }
    [JsonPropertyName("agent")] public required string Agent { get; init; }
    [JsonPropertyName("scope")] public required string Scope { get; init; }
    [JsonPropertyName("exists")] public required bool Exists { get; init; }
}

/// <summary>
/// Source-generated serialization. Required for NativeAOT — the reflection-based
/// serializer is trimmed away and would fail at runtime.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(JsonEnvelope))]
[JsonSerializable(typeof(JsonRootsEnvelope))]
internal sealed partial class SkillMeterJsonContext : JsonSerializerContext;

public static class JsonReporter
{
    public static string Render(BudgetReport report, string toolVersion)
    {
        var envelope = new JsonEnvelope
        {
            ToolVersion = toolVersion,
            Tokenizer = report.Tokenizer,
            Budget = new JsonBudget
            {
                ContextWindow = report.ContextWindow,
                Fraction = report.BudgetFraction,
                BudgetTokens = report.BudgetTokens,
                MaxDescChars = report.MaxDescChars,
                OverBudget = report.IsOverBudget,
                OverageTokens = report.OverageTokens,
                BudgetMultiple = Math.Round(report.BudgetMultiple, 3),
                SkillsThatFit = report.SkillsThatFit,
                SkillsGoingDark = report.SkillsGoingDark,
            },
            Totals = new JsonTotals
            {
                SkillCount = report.Skills.Count,
                ListedSkillCount = report.ListedCount,
                NotListedSkillCount = report.NotListedCount,
                MetadataTokens = report.MetadataTokens,
                BodyTokens = report.BodyTokens,
                ResourceTokens = report.ResourceTokens,
                ResourceFiles = report.ResourceFiles,
                PercentOfWindow = Math.Round(report.PercentOfWindow, 4),
                SkippedCount = report.SkippedCount,
            },
            Skipped = report.Skipped
                .Select(s => new JsonSkipped { Path = s.Path, Reason = s.Reason })
                .ToList(),
            Skills = report.Skills
                .OrderByDescending(s => s.MetadataTokens)
                .Select(s => new JsonSkill
                {
                    Name = s.Name,
                    Path = s.Path,
                    Agent = s.Root.Agent.ToString(),
                    Scope = s.Root.Scope,
                    MetadataTokens = s.MetadataTokens,
                    BodyTokens = s.BodyTokens,
                    BodyLines = s.BodyLines,
                    ResourceTokens = s.ResourceTokens,
                    ResourceFiles = s.ResourceFiles,
                    HasFrontmatter = s.HasFrontmatter,
                    NameMismatch = s.NameMismatch,
                    ListingTruncated = s.ListingTruncated,
                    ExceedsBodyGuidance = s.ExceedsBodyGuidance,
                    CountsTowardListing = s.CountsTowardListing,
                })
                .ToList(),
        };

        return JsonSerializer.Serialize(envelope, SkillMeterJsonContext.Default.JsonEnvelope);
    }

    public static string RenderRoots(IReadOnlyList<SkillRoot> roots, string toolVersion)
    {
        var envelope = new JsonRootsEnvelope
        {
            ToolVersion = toolVersion,
            Roots = roots.Select(r => new JsonRoot
            {
                Path = r.Path,
                Agent = r.Agent.ToString(),
                Scope = r.Scope,
                Exists = Directory.Exists(r.Path),
            }).ToList(),
        };

        return JsonSerializer.Serialize(envelope, SkillMeterJsonContext.Default.JsonRootsEnvelope);
    }
}
