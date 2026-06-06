using System.IO;
using System.Text.Json;
using System.Windows.Input;
using MidiKeyPlayer.Models;

namespace MidiKeyPlayer.Services;

public sealed class MappingService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public List<KeyMapping> CreateDefaultMappings() =>
    [
        Create("C3", Key.Z),
        Create("D3", Key.X),
        Create("E3", Key.C),
        Create("F3", Key.V),
        Create("G3", Key.B),
        Create("A3", Key.N),
        Create("B3", Key.M),
        Create("C4", Key.A),
        Create("D4", Key.S),
        Create("E4", Key.D),
        Create("F4", Key.F),
        Create("G4", Key.G),
        Create("A4", Key.H),
        Create("B4", Key.J),
        Create("C5", Key.Q),
        Create("D5", Key.W),
        Create("E5", Key.E),
        Create("F5", Key.R),
        Create("G5", Key.T),
        Create("A5", Key.Y),
        Create("B5", Key.U)
    ];

    public async Task SaveAsync(string path, IEnumerable<KeyMapping> mappings)
    {
        var normalized = mappings.Select(Normalize).Where(m => m.Key != Key.None).ToList();
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(normalized, JsonOptions));
    }

    public async Task<List<KeyMapping>> LoadAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path);
        var mappings = JsonSerializer.Deserialize<List<KeyMapping>>(json, JsonOptions) ?? [];
        return mappings.Select(Normalize).Where(m => m.Key != Key.None).ToList();
    }

    public Dictionary<int, Key> ToDictionary(IEnumerable<KeyMapping> mappings) =>
        mappings.Select(Normalize)
            .Where(m => m.Key != Key.None)
            .GroupBy(m => m.NoteNumber)
            .ToDictionary(g => g.Key, g => g.First().Key);

    public KeyMapping Normalize(KeyMapping mapping)
    {
        if (!string.IsNullOrWhiteSpace(mapping.NoteName))
        {
            mapping.NoteNumber = NoteNameToNumber(mapping.NoteName);
            mapping.NoteName = NumberToNoteName(mapping.NoteNumber);
        }
        else
        {
            mapping.NoteName = NumberToNoteName(mapping.NoteNumber);
        }

        mapping.KeyName = mapping.KeyName.Trim();
        return mapping;
    }

    public static KeyMapping Create(string noteName, Key key)
    {
        var noteNumber = NoteNameToNumber(noteName);
        return new KeyMapping { NoteNumber = noteNumber, NoteName = NumberToNoteName(noteNumber), Key = key };
    }

    public static string NumberToNoteName(int noteNumber)
    {
        string[] names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
        var octave = (noteNumber / 12) - 1;
        return $"{names[((noteNumber % 12) + 12) % 12]}{octave}";
    }

    public static int NoteNameToNumber(string noteName)
    {
        var trimmed = noteName.Trim().ToUpperInvariant();
        if (trimmed.Length < 2)
        {
            throw new FormatException($"无效音名: {noteName}");
        }

        var nameLength = trimmed.Length > 1 && (trimmed[1] == '#' || trimmed[1] == 'B') ? 2 : 1;
        var pitch = trimmed[..nameLength] switch
        {
            "C" => 0,
            "C#" or "DB" => 1,
            "D" => 2,
            "D#" or "EB" => 3,
            "E" => 4,
            "F" => 5,
            "F#" or "GB" => 6,
            "G" => 7,
            "G#" or "AB" => 8,
            "A" => 9,
            "A#" or "BB" => 10,
            "B" => 11,
            _ => throw new FormatException($"无效音名: {noteName}")
        };

        var octave = int.Parse(trimmed[nameLength..]);
        return (octave + 1) * 12 + pitch;
    }
}
