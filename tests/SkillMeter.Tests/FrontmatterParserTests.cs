using SkillMeter.Scanning;
using Xunit;

namespace SkillMeter.Tests;

public class FrontmatterParserTests
{
    [Fact]
    public void ParsesMinimalSpecExample()
    {
        var (fields, body) = FrontmatterParser.Parse(
            """
            ---
            name: pdf-processing
            description: Extract PDF text, fill forms, merge files. Use when handling PDFs.
            ---
            # Instructions

            Do the thing.
            """);

        Assert.Equal("pdf-processing", fields["name"]);
        Assert.StartsWith("Extract PDF text", fields["description"]);
        Assert.Contains("Do the thing.", body);
        Assert.DoesNotContain("description:", body);
    }

    [Fact]
    public void ParsesAllOptionalSpecFields()
    {
        var (fields, _) = FrontmatterParser.Parse(
            """
            ---
            name: x
            description: d
            license: Apache-2.0
            compatibility: Requires git, docker, jq
            allowed-tools: Bash(git:*) Read
            ---
            body
            """);

        Assert.Equal("Apache-2.0", fields["license"]);
        Assert.Equal("Requires git, docker, jq", fields["compatibility"]);
        Assert.Equal("Bash(git:*) Read", fields["allowed-tools"]);
    }

    [Fact]
    public void FoldsMultiLineDescriptionIntoOneValue()
    {
        var (fields, _) = FrontmatterParser.Parse(
            """
            ---
            name: x
            description: first line
              continued here
              and here
            ---
            body
            """);

        Assert.Equal("first line continued here and here", fields["description"]);
    }

    [Fact]
    public void HandlesBlockScalarDescription()
    {
        var (fields, _) = FrontmatterParser.Parse(
            """
            ---
            name: x
            description: |
              Line one.
              Line two.
            ---
            body
            """);

        Assert.Contains("Line one.", fields["description"]);
        Assert.Contains("Line two.", fields["description"]);
    }

    [Fact]
    public void StripsSurroundingQuotes()
    {
        var (fields, _) = FrontmatterParser.Parse(
            "---\nname: \"quoted-name\"\ndescription: 'single quoted'\n---\nbody");

        Assert.Equal("quoted-name", fields["name"]);
        Assert.Equal("single quoted", fields["description"]);
    }

    [Fact]
    public void ReturnsWholeTextAsBodyWhenNoFrontmatter()
    {
        const string text = "# Just a markdown file\n\nNo frontmatter here.";
        var (fields, body) = FrontmatterParser.Parse(text);

        Assert.Empty(fields);
        Assert.Equal(text, body);
    }

    [Fact]
    public void DoesNotTreatHorizontalRuleAsFrontmatter()
    {
        // A body that merely starts with a thematic break must not be misread.
        const string text = "---\nthis is not frontmatter, there is no closing fence\n";
        var (fields, body) = FrontmatterParser.Parse(text);

        Assert.Empty(fields);
        Assert.Equal(text, body);
    }

    [Fact]
    public void IgnoresCommentsAndBlankLines()
    {
        var (fields, _) = FrontmatterParser.Parse(
            "---\n# a comment\nname: x\n\ndescription: d\n---\nbody");

        Assert.Equal("x", fields["name"]);
        Assert.Equal("d", fields["description"]);
    }

    [Fact]
    public void ToleratesCrLfLineEndings()
    {
        var (fields, body) = FrontmatterParser.Parse(
            "---\r\nname: win\r\ndescription: windows line endings\r\n---\r\nbody text\r\n");

        Assert.Equal("win", fields["name"]);
        Assert.Equal("windows line endings", fields["description"]);
        Assert.Contains("body text", body);
    }

    [Fact]
    public void ToleratesByteOrderMark()
    {
        var (fields, _) = FrontmatterParser.Parse(
            "﻿---\nname: bom\ndescription: d\n---\nbody");

        Assert.Equal("bom", fields["name"]);
    }

    [Fact]
    public void FieldLookupIsCaseInsensitive()
    {
        var (fields, _) = FrontmatterParser.Parse("---\nName: x\nDESCRIPTION: d\n---\nbody");

        Assert.Equal("x", fields["name"]);
        Assert.Equal("d", fields["description"]);
    }
}
