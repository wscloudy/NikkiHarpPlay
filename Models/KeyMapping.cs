using System.Text.Json.Serialization;
using System.Windows.Input;

namespace MidiKeyPlayer.Models;

public sealed class KeyMapping
{
    public int NoteNumber { get; set; }
    public string NoteName { get; set; } = "";
    public string KeyName { get; set; } = "";

    [JsonIgnore]
    public Key Key
    {
        get => Enum.TryParse<Key>(KeyName, true, out var key) ? key : Key.None;
        set => KeyName = value.ToString();
    }
}
