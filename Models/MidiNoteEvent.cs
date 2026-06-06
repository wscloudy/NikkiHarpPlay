namespace MidiKeyPlayer.Models;

public sealed class MidiNoteEvent
{
    public int NoteNumber { get; set; }
    public string NoteName { get; set; } = "";
    public double StartTimeMs { get; set; }
    public double DurationMs { get; set; }
    public int Velocity { get; set; }
    public int TrackIndex { get; set; }

    public MidiNoteEvent Clone() => new()
    {
        NoteNumber = NoteNumber,
        NoteName = NoteName,
        StartTimeMs = StartTimeMs,
        DurationMs = DurationMs,
        Velocity = Velocity,
        TrackIndex = TrackIndex
    };
}
