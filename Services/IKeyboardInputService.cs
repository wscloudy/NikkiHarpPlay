using System.Windows.Input;

namespace MidiKeyPlayer.Services;

public interface IKeyboardInputService
{
    Task TapKeyAsync(Key key, int durationMs, CancellationToken cancellationToken);
}
