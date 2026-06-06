namespace MidiKeyPlayer.Models;

public sealed class PlaybackOptions
{
    public double SpeedMultiplier { get; set; } = 1.0;
    public int OctaveShift { get; set; }
    public int TransposeSemitones { get; set; }
    public HashSet<int> SelectedTracks { get; set; } = [];
    public bool MelodyOnly { get; set; }
    public bool ArpeggioMode { get; set; }
    public bool ArpeggioHighToLow { get; set; }
    public int ArpeggioIntervalMs { get; set; } = 50;
    public int MinNoteDurationMs { get; set; } = 0;
    public int MinKeyIntervalMs { get; set; } = 20;
    public bool FoldOutOfRangeNotes { get; set; } = true;
    public int CountdownSeconds { get; set; } = 3;
    public int TapDurationMs { get; set; } = 30;
}
