using System.IO;

namespace MidiKeyPlayer.Models;

public sealed class MidiDocument
{
    public string FilePath { get; set; } = "";
    public string FileName => Path.GetFileName(FilePath);
    public int FileFormat { get; set; }
    public int TickResolution { get; set; }
    public int TrackCount { get; set; }
    public double TotalDurationMs { get; set; }
    public List<MidiNoteEvent> Notes { get; set; } = [];
    public List<TrackInfo> Tracks { get; set; } = [];
}
