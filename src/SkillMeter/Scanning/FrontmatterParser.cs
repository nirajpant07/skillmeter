using System.Text;

namespace SkillMeter.Scanning;

/// <summary>
/// Minimal YAML frontmatter reader for SKILL.md.
///
/// Deliberately NOT a general YAML parser. The Agent Skills spec defines a small,
/// flat frontmatter (name, description, license, compatibility, metadata,
/// allowed-tools), so a full YAML dependency would be cost without benefit — and
/// the .NET YAML libraries are reflection-heavy, which NativeAOT disallows.
///
/// It does handle the constructs that actually occur in published skills, because
/// getting them wrong inflates the headline token count:
///   - literal block scalars (|, |-, |+)   newlines preserved
///   - folded block scalars (>, >-, >+)    newlines folded to spaces
///   - plain multi-line scalars            folded to spaces
///   - single and double quoted scalars, with escapes unescaped
///   - full-line and inline comments
///
/// Deliberately unsupported: anchors, aliases, tags, flow collections, multiple
/// documents, nested mappings beyond depth 1. None appear in the spec's field set.
/// </summary>
public static class FrontmatterParser
{
    private enum ScalarStyle { Plain, Literal, Folded }

    /// <summary>
    /// Splits a SKILL.md into its frontmatter fields and body.
    /// Returns an empty dictionary and the whole text as body when there is no frontmatter.
    /// </summary>
    public static (Dictionary<string, string> Fields, string Body) Parse(string text)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(text)) return (fields, text);

        // File.ReadAllText strips a real UTF-8 BOM; this catches a doubled one.
        var span = text.AsSpan();
        if (span.Length > 0 && span[0] == '﻿') span = span[1..];

        if (!span.StartsWith("---")) return (fields, text);

        // The opening --- must be alone on its line.
        var afterOpen = span[3..];
        var firstNewline = afterOpen.IndexOf('\n');
        if (firstNewline < 0 || afterOpen[..firstNewline].Trim().Length != 0)
            return (fields, text);

        var rest = afterOpen[(firstNewline + 1)..].ToString();
        var lines = rest.Split('\n');

        // YAML requires the closing fence at column 0. Trimming before comparing
        // would let an indented "---" inside a block scalar end the frontmatter
        // early, silently truncating the description and spilling keys into the body.
        var fenceLine = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line is "---" or "...") { fenceLine = i; break; }
        }

        if (fenceLine < 0) return (fields, text);

        ParseBlock(lines.AsSpan(0, fenceLine), fields);

        var body = fenceLine + 1 < lines.Length
            ? string.Join('\n', lines[(fenceLine + 1)..])
            : "";

        return (fields, body);
    }

    private static void ParseBlock(ReadOnlySpan<string> lines, Dictionary<string, string> fields)
    {
        string? key = null;
        var style = ScalarStyle.Plain;
        var parts = new List<string>();

        void Flush()
        {
            if (key is not null) fields[key] = Assemble(parts, style);
            parts.Clear();
        }

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');

            // Inside a block scalar every indented line is content, including
            // things that look like comments or keys.
            var indented = line.Length > 0 && char.IsWhiteSpace(line[0]);
            if (key is not null && style != ScalarStyle.Plain && (indented || line.Length == 0))
            {
                parts.Add(line.Trim());
                continue;
            }

            if (line.Trim().Length == 0) continue;

            // A full-line comment is never part of a value. The previous version
            // only skipped these before the first key, so a comment further down
            // was appended to whichever value preceded it.
            if (line.TrimStart().StartsWith('#')) continue;

            var k = "";
            var v = "";
            var isTopLevel = !indented && SplitKey(line, out k, out v);

            if (isTopLevel)
            {
                Flush();
                key = k;

                if (v.StartsWith('|')) { style = ScalarStyle.Literal; }
                else if (v.StartsWith('>')) { style = ScalarStyle.Folded; }
                else { style = ScalarStyle.Plain; parts.Add(StripInlineComment(v)); }
            }
            else if (key is not null)
            {
                // Continuation of a plain multi-line scalar, or a nested mapping we flatten.
                parts.Add(StripInlineComment(line.Trim()));
            }
        }

        Flush();
    }

    /// <summary>Splits "key: value" at the first colon that is followed by space or end of line.</summary>
    private static bool SplitKey(string line, out string key, out string value)
    {
        key = "";
        value = "";

        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] != ':') continue;
            if (i + 1 < line.Length && line[i + 1] != ' ' && line[i + 1] != '\t') continue;

            key = line[..i].Trim();
            value = line[(i + 1)..].Trim();
            return key.Length > 0 && !key.Contains(' ');
        }

        return false;
    }

    /// <summary>
    /// Removes a trailing " # comment". Only strips when the hash follows whitespace
    /// and sits outside quotes, so "a#b" and "a # b" behave as YAML says they should.
    /// </summary>
    private static string StripInlineComment(string value)
    {
        var inSingle = false;
        var inDouble = false;

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (c == '\\' && inDouble) { i++; continue; }
            if (c == '\'' && !inDouble) inSingle = !inSingle;
            else if (c == '"' && !inSingle) inDouble = !inDouble;
            else if (c == '#' && !inSingle && !inDouble && i > 0 && char.IsWhiteSpace(value[i - 1]))
                return value[..i].TrimEnd();
        }

        return value;
    }

    private static string Assemble(List<string> parts, ScalarStyle style)
    {
        var joined = style switch
        {
            // Literal keeps line structure.
            ScalarStyle.Literal => string.Join('\n', parts).Trim(),

            // Folded and plain both collapse to spaces. Treating folded as literal
            // was the single largest source of over-counting: 116 of 235 corpus
            // descriptions use ">" or ">-" against 1 using "|-".
            _ => string.Join(' ', parts.Where(p => p.Length > 0)).Trim(),
        };

        return Unquote(joined);
    }

    private static string Unquote(string value)
    {
        if (value.Length < 2) return value;

        var first = value[0];
        var last = value[^1];
        if (first != last || (first != '"' && first != '\'')) return value;

        var inner = value[1..^1];

        // A quote in the middle means these were not wrapping quotes at all,
        // e.g.  description: "a" and "b"
        if (first == '\'')
        {
            return inner.Contains('\'') ? value : inner;
        }

        var unescaped = new StringBuilder(inner.Length);
        for (var i = 0; i < inner.Length; i++)
        {
            var c = inner[i];

            if (c == '"') return value; // unescaped interior quote: not a wrapped scalar

            if (c != '\\' || i + 1 >= inner.Length) { unescaped.Append(c); continue; }

            var next = inner[++i];
            unescaped.Append(next switch
            {
                'n' => '\n',
                't' => '\t',
                'r' => '\r',
                '0' => '\0',
                '\\' => '\\',
                '"' => '"',
                '/' => '/',
                _ => next,
            });
        }

        return unescaped.ToString();
    }
}
