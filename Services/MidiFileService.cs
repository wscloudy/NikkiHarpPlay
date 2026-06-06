using MidiKeyPlayer.Models;
using NAudio.Midi;

namespace MidiKeyPlayer.Services;

public sealed class MidiFileService
{
    private const int DefaultTempoMicrosecondsPerQuarter = 500_000;

    public MidiDocument Load(string filePath, Action<string>? log = null)
    {
        var midi = new MidiFile(filePath, false);
        var tempoMap = BuildTempoMap(midi);
        var notes = new List<MidiNoteEvent>();

        for (var trackIndex = 0; trackIndex < midi.Tracks; trackIndex++)
        {
            var activeNotes = new Dictionary<(int Channel, int Note), Queue<NoteOnEvent>>();

            foreach (var midiEvent in midi.Events[trackIndex].OrderBy(e => e.AbsoluteTime))
            {
                if (midiEvent is NoteOnEvent { Velocity: > 0 } noteOn)
                {
                    var key = (noteOn.Channel, noteOn.NoteNumber);
                    if (!activeNotes.TryGetValue(key, out var queue))
                    {
                        queue = new Queue<NoteOnEvent>();
                        activeNotes[key] = queue;
                    }

                    queue.Enqueue(noteOn);
                    continue;
                }

                if (IsNoteOff(midiEvent, out var channel, out var noteNumber))
                {
                    var key = (channel, noteNumber);
                    if (!activeNotes.TryGetValue(key, out var queue) || queue.Count == 0)
                    {
                        continue;
                    }

                    var start = queue.Dequeue();
                    var startMs = TicksToMs(start.AbsoluteTime, tempoMap, midi.DeltaTicksPerQuarterNote);
                    var endMs = TicksToMs(midiEvent.AbsoluteTime, tempoMap, midi.DeltaTicksPerQuarterNote);
                    notes.Add(new MidiNoteEvent
                    {
                        NoteNumber = noteNumber,
                        NoteName = MappingService.NumberToNoteName(noteNumber),
                        StartTimeMs = startMs,
                        DurationMs = Math.Max(1, endMs - startMs),
                        Velocity = start.Velocity,
                        TrackIndex = trackIndex
                    });
                }
            }

            foreach (var pending in activeNotes.Values.SelectMany(queue => queue))
            {
                // MIDI 文件偶尔会缺失 NoteOff；这里保守跳过，避免播放无限长音。
                log?.Invoke($"轨道 {trackIndex}: 跳过未闭合音符 {MappingService.NumberToNoteName(pending.NoteNumber)}。");
            }
        }

        notes = notes.OrderBy(n => n.StartTimeMs).ThenBy(n => n.NoteNumber).ToList();
        return new MidiDocument
        {
            FilePath = filePath,
            FileFormat = midi.FileFormat,
            TickResolution = midi.DeltaTicksPerQuarterNote,
            TrackCount = midi.Tracks,
            TotalDurationMs = notes.Count == 0 ? 0 : notes.Max(n => n.StartTimeMs + n.DurationMs),
            Notes = notes,
            Tracks = Enumerable.Range(0, midi.Tracks)
                .Select(i => new TrackInfo
                {
                    TrackIndex = i,
                    NoteCount = notes.Count(n => n.TrackIndex == i),
                    IsSelected = notes.Any(n => n.TrackIndex == i)
                })
                .ToList()
        };
    }

    private static bool IsNoteOff(MidiEvent midiEvent, out int channel, out int noteNumber)
    {
        switch (midiEvent)
        {
            case NoteEvent { CommandCode: MidiCommandCode.NoteOff } noteOff:
                channel = noteOff.Channel;
                noteNumber = noteOff.NoteNumber;
                return true;
            case NoteOnEvent { Velocity: 0 } noteOnAsOff:
                channel = noteOnAsOff.Channel;
                noteNumber = noteOnAsOff.NoteNumber;
                return true;
            default:
                channel = 0;
                noteNumber = 0;
                return false;
        }
    }

    private static List<TempoPoint> BuildTempoMap(MidiFile midi)
    {
        var points = midi.Events
            .SelectMany(track => track)
            .OfType<TempoEvent>()
            .OrderBy(e => e.AbsoluteTime)
            .Select(e => new TempoPoint(e.AbsoluteTime, e.MicrosecondsPerQuarterNote))
            .ToList();

        if (points.Count == 0 || points[0].Tick != 0)
        {
            points.Insert(0, new TempoPoint(0, DefaultTempoMicrosecondsPerQuarter));
        }

        return points;
    }

    private static double TicksToMs(long targetTick, IReadOnlyList<TempoPoint> tempoMap, int ticksPerQuarter)
    {
        double totalMs = 0;

        for (var i = 0; i < tempoMap.Count; i++)
        {
            var current = tempoMap[i];
            var nextTick = i + 1 < tempoMap.Count ? tempoMap[i + 1].Tick : targetTick;
            var segmentEnd = Math.Min(targetTick, nextTick);

            if (segmentEnd > current.Tick)
            {
                var ticks = segmentEnd - current.Tick;
                totalMs += ticks * current.MicrosecondsPerQuarter / (double)ticksPerQuarter / 1000.0;
            }

            if (targetTick < nextTick)
            {
                break;
            }
        }

        return totalMs;
    }

    private sealed record TempoPoint(long Tick, int MicrosecondsPerQuarter);
}
