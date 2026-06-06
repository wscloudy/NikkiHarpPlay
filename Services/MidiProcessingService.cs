using System.Windows.Input;
using MidiKeyPlayer.Models;

namespace MidiKeyPlayer.Services;

public sealed class MidiProcessingService
{
    public List<MidiNoteEvent> Process(
        IEnumerable<MidiNoteEvent> source,
        PlaybackOptions options,
        IReadOnlyDictionary<int, Key> mappings,
        Action<string>? log = null)
    {
        var notes = source
            .Where(n => options.SelectedTracks.Count == 0 || options.SelectedTracks.Contains(n.TrackIndex))
            .Where(n => n.DurationMs >= options.MinNoteDurationMs)
            .Select(n => ApplyTransposeAndFold(n, options, mappings, log))
            .Where(n => n is not null)
            .Select(n => n!)
            .OrderBy(n => n.StartTimeMs)
            .ThenBy(n => n.NoteNumber)
            .ToList();

        if (options.MelodyOnly)
        {
            notes = notes.GroupBy(n => Math.Round(n.StartTimeMs))
                .Select(g => g.OrderByDescending(n => n.NoteNumber).First())
                .OrderBy(n => n.StartTimeMs)
                .ToList();
        }
        else if (options.ArpeggioMode)
        {
            notes = ApplyArpeggio(notes, options);
        }

        EnforceMinimumKeyInterval(notes, options.MinKeyIntervalMs);
        return notes;
    }

    private static MidiNoteEvent? ApplyTransposeAndFold(
        MidiNoteEvent note,
        PlaybackOptions options,
        IReadOnlyDictionary<int, Key> mappings,
        Action<string>? log)
    {
        var result = note.Clone();
        // 先做整首歌的八度位移，再叠加半音移调。
        // 例如 OctaveShift = -1 时，C6 会先变成 C5，从而命中 C5 -> Q。
        result.NoteNumber += options.OctaveShift * 12 + options.TransposeSemitones;
        result.NoteName = MappingService.NumberToNoteName(result.NoteNumber);

        if (mappings.ContainsKey(result.NoteNumber))
        {
            return result;
        }

        if (options.FoldOutOfRangeNotes && mappings.Count > 0)
        {
            for (var octaveOffset = 1; octaveOffset <= 8; octaveOffset++)
            {
                var down = result.NoteNumber - 12 * octaveOffset;
                if (mappings.ContainsKey(down))
                {
                    result.NoteNumber = down;
                    result.NoteName = MappingService.NumberToNoteName(down);
                    return result;
                }

                var up = result.NoteNumber + 12 * octaveOffset;
                if (mappings.ContainsKey(up))
                {
                    result.NoteNumber = up;
                    result.NoteName = MappingService.NumberToNoteName(up);
                    return result;
                }
            }
        }

        log?.Invoke($"跳过未映射音符 {result.NoteName} (轨道 {result.TrackIndex})。");
        return null;
    }

    private static List<MidiNoteEvent> ApplyArpeggio(List<MidiNoteEvent> notes, PlaybackOptions options)
    {
        var result = new List<MidiNoteEvent>();
        foreach (var group in notes.GroupBy(n => Math.Round(n.StartTimeMs)).OrderBy(g => g.Key))
        {
            var ordered = options.ArpeggioHighToLow
                ? group.OrderByDescending(n => n.NoteNumber).ToList()
                : group.OrderBy(n => n.NoteNumber).ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                var clone = ordered[i].Clone();
                clone.StartTimeMs += i * options.ArpeggioIntervalMs;
                result.Add(clone);
            }
        }

        return result.OrderBy(n => n.StartTimeMs).ThenBy(n => n.NoteNumber).ToList();
    }

    private static void EnforceMinimumKeyInterval(List<MidiNoteEvent> notes, int minimumMs)
    {
        if (minimumMs <= 0 || notes.Count < 2)
        {
            return;
        }

        var previous = notes[0].StartTimeMs;
        for (var i = 1; i < notes.Count; i++)
        {
            if (notes[i].StartTimeMs - previous < minimumMs)
            {
                notes[i].StartTimeMs = previous + minimumMs;
            }

            previous = notes[i].StartTimeMs;
        }
    }
}
