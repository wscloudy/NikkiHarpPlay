namespace MidiKeyPlayer.Models;

public sealed class TrackInfo
{
    public int TrackIndex { get; set; }
    public int NoteCount { get; set; }
    public bool IsSelected { get; set; } = true;
}
