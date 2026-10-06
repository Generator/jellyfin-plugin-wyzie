using System;
using System.Collections.Generic;

namespace Wyzie.Common;

/// <summary>
/// Strips Wyzie's free-plan ad cue ("store.wyzie.io" at 00:00:00,000).
/// Pure string transform so it stays unit-testable without I/O.
/// SRT/VTT cues are renumbered after removal so the file stays ffprobe-valid
/// (Wyzie files often leave the next cue unnumbered once the head is cut).
/// </summary>
public static class WyzieSubtitleCleaner
{
    private const string AdMarker = "store.wyzie.io";

    private const string ZeroTimestamp = "00:00";

    private const string CueArrow = "-->";

    public static string StripAdCue(string content)
    {
        if (string.IsNullOrEmpty(content)
            || content!.IndexOf(AdMarker, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return content;
        }

        var newline = content.Contains("\r\n") ? "\r\n" : "\n";
        var normalized = content.Replace("\r\n", "\n");

        var stripped = normalized.Contains(CueArrow)
            ? StripCueBased(normalized)
            : StripLineBased(normalized);

        return stripped.Replace("\n", newline);
    }

    private static string StripCueBased(string normalized)
    {
        var blocks = normalized.Split(new[] { "\n\n" }, StringSplitOptions.None);
        var kept = new List<string>(blocks.Length);
        var cueNumber = 1;

        foreach (var block in blocks)
        {
            if (IsAdCue(block))
            {
                continue;
            }

            kept.Add(Renumber(block, ref cueNumber));
        }

        return string.Join("\n\n", kept);
    }

    private static string StripLineBased(string normalized)
    {
        var lines = normalized.Split('\n');
        var kept = new List<string>(lines.Length);

        foreach (var line in lines)
        {
            if (line.IndexOf(AdMarker, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            kept.Add(line);
        }

        return string.Join("\n", kept);
    }

    private static string Renumber(string block, ref int cueNumber)
    {
        if (block.StartsWith("WEBVTT", StringComparison.Ordinal))
        {
            return block;
        }

        var lines = block.Split('\n');
        if (lines.Length == 0)
        {
            return block;
        }

        if (IsIndex(lines[0]))
        {
            lines[0] = cueNumber.ToString();
        }
        else if (lines[0].Contains(CueArrow))
        {
            var renumbered = new string[lines.Length + 1];
            renumbered[0] = cueNumber.ToString();
            Array.Copy(lines, 0, renumbered, 1, lines.Length);
            cueNumber++;
            return string.Join("\n", renumbered);
        }
        else
        {
            return block;
        }

        cueNumber++;
        return string.Join("\n", lines);
    }

    private static bool IsIndex(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        foreach (var c in trimmed)
        {
            if (c < '0' || c > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAdCue(string block)
    {
        return block.IndexOf(AdMarker, StringComparison.OrdinalIgnoreCase) >= 0
            && block.IndexOf(ZeroTimestamp, StringComparison.Ordinal) >= 0;
    }
}
