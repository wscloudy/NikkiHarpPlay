using System.Diagnostics;
using System.Windows.Input;
using MidiKeyPlayer.Models;

namespace MidiKeyPlayer.Services;

public sealed class PlaybackService
{
    private CancellationTokenSource? _playbackCts;
    private readonly ManualResetEventSlim _pauseGate = new(true);

    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    public event Action<double, double>? ProgressChanged;
    public event Action<string>? Log;
    public event Action? PlaybackFinished;

    public async Task StartAsync(
        IReadOnlyList<MidiNoteEvent> notes,
        IReadOnlyDictionary<int, Key> mappings,
        PlaybackOptions options,
        IKeyboardInputService keyboardInputService)
    {
        Stop();
        _playbackCts = new CancellationTokenSource();
        var token = _playbackCts.Token;
        IsPlaying = true;
        IsPaused = false;
        _pauseGate.Set();

        try
        {
            for (var seconds = options.CountdownSeconds; seconds > 0; seconds--)
            {
                Log?.Invoke($"{seconds} 秒后开始，请切换到游戏窗口。");
                await Task.Delay(1000, token);
            }

            var totalMs = notes.Count == 0 ? 0 : notes.Max(n => n.StartTimeMs) / options.SpeedMultiplier;
            var stopwatch = Stopwatch.StartNew();
            var pausedOffsetMs = 0.0;
            var lastProgressReport = 0.0;

            foreach (var note in notes)
            {
                token.ThrowIfCancellationRequested();
                WaitWhilePaused(token, ref pausedOffsetMs);

                var targetMs = note.StartTimeMs / options.SpeedMultiplier;
                await DelayUntilAsync(stopwatch, () => pausedOffsetMs, targetMs, token);
                WaitWhilePaused(token, ref pausedOffsetMs);

                if (!mappings.TryGetValue(note.NoteNumber, out var key))
                {
                    Log?.Invoke($"跳过未映射音符 {note.NoteName}。");
                    continue;
                }

                await keyboardInputService.TapKeyAsync(key, options.TapDurationMs, token);

                if (targetMs - lastProgressReport > 50)
                {
                    lastProgressReport = targetMs;
                    ProgressChanged?.Invoke(Math.Min(targetMs, totalMs), totalMs);
                }
            }

            ProgressChanged?.Invoke(totalMs, totalMs);
            Log?.Invoke("演奏完成。");
        }
        catch (OperationCanceledException)
        {
            Log?.Invoke("演奏已停止。");
        }
        catch (Exception ex)
        {
            Log?.Invoke($"播放异常: {ex.Message}");
        }
        finally
        {
            IsPlaying = false;
            IsPaused = false;
            _pauseGate.Set();
            PlaybackFinished?.Invoke();
        }
    }

    public void Stop()
    {
        _playbackCts?.Cancel();
        _pauseGate.Set();
    }

    public void Pause()
    {
        if (!IsPlaying)
        {
            return;
        }

        IsPaused = true;
        _pauseGate.Reset();
        Log?.Invoke("已暂停。");
    }

    public void Resume()
    {
        if (!IsPlaying)
        {
            return;
        }

        IsPaused = false;
        _pauseGate.Set();
        Log?.Invoke("继续播放。");
    }

    private void WaitWhilePaused(CancellationToken token, ref double pausedOffsetMs)
    {
        var pauseStarted = IsPaused ? DateTime.UtcNow : (DateTime?)null;

        while (!_pauseGate.Wait(50))
        {
            token.ThrowIfCancellationRequested();
        }

        if (pauseStarted is not null)
        {
            pausedOffsetMs += (DateTime.UtcNow - pauseStarted.Value).TotalMilliseconds;
        }
    }

    private static async Task DelayUntilAsync(Stopwatch stopwatch, Func<double> getPausedOffsetMs, double targetMs, CancellationToken token)
    {
        while (stopwatch.Elapsed.TotalMilliseconds - getPausedOffsetMs() < targetMs)
        {
            var remaining = targetMs - (stopwatch.Elapsed.TotalMilliseconds - getPausedOffsetMs());
            await Task.Delay((int)Math.Clamp(remaining, 1, 20), token);
        }
    }
}
