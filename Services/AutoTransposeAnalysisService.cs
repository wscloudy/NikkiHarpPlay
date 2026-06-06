using System.Windows.Input;
using MidiKeyPlayer.Models;

namespace MidiKeyPlayer.Services;

public sealed class AutoTransposeAnalysisService
{
    public TransposeAnalysisResult FindBestTranspose(
        IEnumerable<MidiNoteEvent> notes,
        IReadOnlyDictionary<int, Key> mappings,
        int octaveShift,
        bool foldOutOfRangeNotes,
        int minTranspose = -12,
        int maxTranspose = 12)
    {
        var noteList = notes.ToList();
        if (noteList.Count == 0 || mappings.Count == 0)
        {
            return new TransposeAnalysisResult(0, 0, noteList.Count, 0);
        }

        return Enumerable.Range(minTranspose, maxTranspose - minTranspose + 1)
            .Select(transpose => ScoreTranspose(noteList, mappings, octaveShift, foldOutOfRangeNotes, transpose))
            .OrderByDescending(result => result.MappedCount)
            .ThenBy(result => result.FoldedCount)
            .ThenBy(result => Math.Abs(result.TransposeSemitones))
            .First();
    }

    private static TransposeAnalysisResult ScoreTranspose(
        IReadOnlyList<MidiNoteEvent> notes,
        IReadOnlyDictionary<int, Key> mappings,
        int octaveShift,
        bool foldOutOfRangeNotes,
        int transpose)
    {
        var mapped = 0;
        var folded = 0;

        foreach (var note in notes)
        {
            var shifted = note.NoteNumber + octaveShift * 12 + transpose;
            if (mappings.ContainsKey(shifted))
            {
                mapped++;
                continue;
            }

            if (!foldOutOfRangeNotes)
            {
                continue;
            }

            if (CanFoldToMapping(shifted, mappings))
            {
                mapped++;
                folded++;
            }
        }

        return new TransposeAnalysisResult(transpose, mapped, notes.Count, folded);
    }

    private static bool CanFoldToMapping(int noteNumber, IReadOnlyDictionary<int, Key> mappings)
    {
        for (var octaveOffset = 1; octaveOffset <= 8; octaveOffset++)
        {
            if (mappings.ContainsKey(noteNumber - 12 * octaveOffset) ||
                mappings.ContainsKey(noteNumber + 12 * octaveOffset))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed record TransposeAnalysisResult(
    int TransposeSemitones,
    int MappedCount,
    int TotalCount,
    int FoldedCount)
{
    public double MappedRatio => TotalCount == 0 ? 0 : MappedCount / (double)TotalCount;
}
