using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using MidiKeyPlayer.Models;
using MidiKeyPlayer.Services;

namespace MidiKeyPlayer.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly MidiFileService _midiFileService = new();
    private readonly MidiProcessingService _processingService = new();
    private readonly MappingService _mappingService = new();
    private readonly AutoTransposeAnalysisService _autoTransposeAnalysisService = new();
    private readonly PlaybackService _playbackService = new();
    private readonly KeyboardInputService _sendInputService = new();
    private readonly GameWindowMessageInputService _gameWindowMessageInputService = new();
    private MidiDocument? _document;
    private string _filePath = "";
    private string _fileInfo = "尚未加载 MIDI 文件。";
    private string _logText = "";
    private double _progressValue;
    private double _progressMaximum = 1;
    private string _timeText = "00:00.000 / 00:00.000";
    private bool _isPlaying;
    private bool _isPaused;
    private double _speedMultiplier = 1.0;
    private int _octaveShift;
    private int _transposeSemitones;
    private int _countdownSeconds = 3;
    private bool _melodyOnly;
    private bool _arpeggioMode;
    private bool _arpeggioHighToLow;
    private int _arpeggioIntervalMs = 50;
    private int _minNoteDurationMs = 0;
    private int _minKeyIntervalMs = 20;
    private bool _foldOutOfRangeNotes = true;
    private int _tapDurationMs = 30;
    private bool _autoTuneOnOpen = true;
    //无限暖暖模式
    private bool _useGameWindowMessageMode = true; 

    public MainViewModel()
    {
        TrackInfos = [];
        Mappings = new ObservableCollection<KeyMapping>(_mappingService.CreateDefaultMappings());

        SelectMidiCommand = new RelayCommand(_ => SelectMidi());
        StartCommand = new RelayCommand(async _ => await StartAsync(), _ => _document is not null && !IsPlaying);
        StopCommand = new RelayCommand(_ => Stop(), _ => IsPlaying);
        PauseResumeCommand = new RelayCommand(_ => PauseResume(), _ => IsPlaying);
        SaveMappingsCommand = new RelayCommand(async _ => await SaveMappingsAsync());
        LoadMappingsCommand = new RelayCommand(async _ => await LoadMappingsAsync());
        AddMappingCommand = new RelayCommand(_ => Mappings.Add(MappingService.Create("C4", Key.A)));
        RemoveMappingCommand = new RelayCommand(_ =>
        {
            if (SelectedMapping is not null)
            {
                Mappings.Remove(SelectedMapping);
            }
        });

        _playbackService.Log += AppendLog;
        _playbackService.ProgressChanged += (current, total) =>
        {
            RunOnUi(() =>
            {
                ProgressMaximum = Math.Max(1, total);
                ProgressValue = Math.Clamp(current, 0, ProgressMaximum);
                TimeText = $"{FormatTime(current)} / {FormatTime(total)}";
            });
        };
        _playbackService.PlaybackFinished += () =>
        {
            RunOnUi(() =>
            {
                IsPlaying = false;
                IsPaused = false;
                OnPropertyChanged(nameof(PauseResumeText));
                RaiseCommandStates();
            });
        };
    }

    public ObservableCollection<TrackInfo> TrackInfos { get; }
    public ObservableCollection<KeyMapping> Mappings { get; }
    public IReadOnlyList<double> SpeedOptions { get; } = [0.5, 0.75, 1.0, 1.25, 1.5];

    public ICommand SelectMidiCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand PauseResumeCommand { get; }
    public ICommand SaveMappingsCommand { get; }
    public ICommand LoadMappingsCommand { get; }
    public ICommand AddMappingCommand { get; }
    public ICommand RemoveMappingCommand { get; }

    public KeyMapping? SelectedMapping { get; set; }

    public string FilePath { get => _filePath; set => SetProperty(ref _filePath, value); }
    public string FileInfo { get => _fileInfo; set => SetProperty(ref _fileInfo, value); }
    public string LogText { get => _logText; set => SetProperty(ref _logText, value); }
    public double ProgressValue { get => _progressValue; set => SetProperty(ref _progressValue, value); }
    public double ProgressMaximum { get => _progressMaximum; set => SetProperty(ref _progressMaximum, value); }
    public string TimeText { get => _timeText; set => SetProperty(ref _timeText, value); }
    public bool IsPlaying { get => _isPlaying; private set => SetProperty(ref _isPlaying, value); }
    public bool IsPaused { get => _isPaused; private set => SetProperty(ref _isPaused, value); }
    public string PauseResumeText => IsPaused ? "继续" : "暂停";

    public double SpeedMultiplier { get => _speedMultiplier; set => SetProperty(ref _speedMultiplier, value); }
    public int OctaveShift { get => _octaveShift; set => SetProperty(ref _octaveShift, value); }
    public int TransposeSemitones { get => _transposeSemitones; set => SetProperty(ref _transposeSemitones, value); }
    public int CountdownSeconds { get => _countdownSeconds; set => SetProperty(ref _countdownSeconds, value); }
    public bool MelodyOnly { get => _melodyOnly; set => SetProperty(ref _melodyOnly, value); }
    public bool ArpeggioMode { get => _arpeggioMode; set => SetProperty(ref _arpeggioMode, value); }
    public bool ArpeggioHighToLow { get => _arpeggioHighToLow; set => SetProperty(ref _arpeggioHighToLow, value); }
    public int ArpeggioIntervalMs { get => _arpeggioIntervalMs; set => SetProperty(ref _arpeggioIntervalMs, value); }
    public int MinNoteDurationMs { get => _minNoteDurationMs; set => SetProperty(ref _minNoteDurationMs, value); }
    public int MinKeyIntervalMs { get => _minKeyIntervalMs; set => SetProperty(ref _minKeyIntervalMs, value); }
    public bool FoldOutOfRangeNotes { get => _foldOutOfRangeNotes; set => SetProperty(ref _foldOutOfRangeNotes, value); }
    public int TapDurationMs { get => _tapDurationMs; set => SetProperty(ref _tapDurationMs, value); }
    public bool AutoTuneOnOpen { get => _autoTuneOnOpen; set => SetProperty(ref _autoTuneOnOpen, value); }
    public bool UseGameWindowMessageMode { get => _useGameWindowMessageMode; set => SetProperty(ref _useGameWindowMessageMode, value); }

    public void StopFromHotkey()
    {
        AppendLog("收到 F8 全局停止热键。");
        Stop();
    }

    private void SelectMidi()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "MIDI 文件 (*.mid;*.midi)|*.mid;*.midi|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            LogText = "";
            _document = _midiFileService.Load(dialog.FileName, AppendLog);
            FilePath = dialog.FileName;
            TrackInfos.Clear();
            foreach (var track in _document.Tracks)
            {
                TrackInfos.Add(track);
            }

            FileInfo = $"文件名: {_document.FileName} | 格式: {_document.FileFormat} | Tick 分辨率: {_document.TickResolution} | 轨道: {_document.TrackCount} | 音符: {_document.Notes.Count}";
            ProgressMaximum = Math.Max(1, _document.TotalDurationMs);
            ProgressValue = 0;
            TimeText = $"{FormatTime(0)} / {FormatTime(_document.TotalDurationMs)}";
            AppendLog("MIDI 读取完成。");
            ApplyAutoTuneIfEnabled();
            RaiseCommandStates();
        }
        catch (Exception ex)
        {
            AppendLog($"读取失败: {ex.Message}");
        }
    }

    private async Task StartAsync()
    {
        if (_document is null)
        {
            return;
        }

        try
        {
            var mappings = _mappingService.ToDictionary(Mappings);
            var options = BuildPlaybackOptions();
            var processedNotes = _processingService.Process(_document.Notes, options, mappings, AppendLog);

            if (processedNotes.Count == 0)
            {
                AppendLog("没有可播放音符，请检查轨道选择和映射表。");
                return;
            }

            IsPlaying = true;
            IsPaused = false;
            OnPropertyChanged(nameof(PauseResumeText));
            RaiseCommandStates();
            IKeyboardInputService inputService = UseGameWindowMessageMode
                ? _gameWindowMessageInputService
                : _sendInputService;
            var inputModeName = UseGameWindowMessageMode ? "无限暖暖窗口消息模式" : "SendInput 前台模式";
            AppendLog($"准备播放 {processedNotes.Count} 个音符。输入模式: {inputModeName}。F8 可全局停止。");
            // 播放循环必须离开 UI 线程，否则密集音符和 SendInput 会让窗口失去响应。
            await Task.Run(() => _playbackService.StartAsync(processedNotes, mappings, options, inputService));
        }
        catch (Exception ex)
        {
            AppendLog($"启动失败: {ex.Message}");
            IsPlaying = false;
            RaiseCommandStates();
        }
    }

    private PlaybackOptions BuildPlaybackOptions() => new()
    {
        SpeedMultiplier = Math.Clamp(SpeedMultiplier, 0.1, 4.0),
        OctaveShift = Math.Clamp(OctaveShift, -4, 4),
        TransposeSemitones = Math.Clamp(TransposeSemitones, -12, 12),
        SelectedTracks = TrackInfos.Where(t => t.IsSelected).Select(t => t.TrackIndex).ToHashSet(),
        MelodyOnly = MelodyOnly,
        ArpeggioMode = ArpeggioMode,
        ArpeggioHighToLow = ArpeggioHighToLow,
        ArpeggioIntervalMs = Math.Max(0, ArpeggioIntervalMs),
        MinNoteDurationMs = Math.Max(0, MinNoteDurationMs),
        MinKeyIntervalMs = Math.Max(0, MinKeyIntervalMs),
        FoldOutOfRangeNotes = FoldOutOfRangeNotes,
        CountdownSeconds = Math.Clamp(CountdownSeconds, 0, 10),
        TapDurationMs = Math.Clamp(TapDurationMs, 1, 500)
    };

    private void ApplyAutoTuneIfEnabled()
    {
        if (!AutoTuneOnOpen || _document is null || _document.Notes.Count == 0)
        {
            return;
        }

        try
        {
            var mappings = _mappingService.ToDictionary(Mappings);
            var selectedTracks = TrackInfos.Where(t => t.IsSelected).Select(t => t.TrackIndex).ToHashSet();
            var notes = _document.Notes
                .Where(n => selectedTracks.Count == 0 || selectedTracks.Contains(n.TrackIndex))
                .ToList();
            var result = _autoTransposeAnalysisService.FindBestTranspose(
                notes,
                mappings,
                Math.Clamp(OctaveShift, -4, 4),
                FoldOutOfRangeNotes);

            TransposeSemitones = result.TransposeSemitones;
            AppendLog($"自动调音: 推荐移调 {result.TransposeSemitones:+#;-#;0} 半音，命中 {result.MappedCount}/{result.TotalCount}，折叠 {result.FoldedCount}。");
        }
        catch (Exception ex)
        {
            AppendLog($"自动调音失败: {ex.Message}");
        }
    }

    private void Stop()
    {
        _playbackService.Stop();
        IsPlaying = false;
        IsPaused = false;
        OnPropertyChanged(nameof(PauseResumeText));
        RaiseCommandStates();
    }

    private void PauseResume()
    {
        if (IsPaused)
        {
            _playbackService.Resume();
            IsPaused = false;
        }
        else
        {
            _playbackService.Pause();
            IsPaused = true;
        }

        OnPropertyChanged(nameof(PauseResumeText));
    }

    private async Task SaveMappingsAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON 映射 (*.json)|*.json",
            FileName = "mapping.json"
        };

        if (dialog.ShowDialog() == true)
        {
            await _mappingService.SaveAsync(dialog.FileName, Mappings);
            AppendLog($"映射已保存: {dialog.FileName}");
        }
    }

    private async Task LoadMappingsAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON 映射 (*.json)|*.json"
        };

        if (dialog.ShowDialog() == true)
        {
            var loaded = await _mappingService.LoadAsync(dialog.FileName);
            Mappings.Clear();
            foreach (var mapping in loaded)
            {
                Mappings.Add(mapping);
            }

            AppendLog($"映射已加载: {dialog.FileName}");
        }
    }

    private void AppendLog(string message)
    {
        RunOnUi(() =>
        {
            LogText += $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        });
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }

    private void RaiseCommandStates()
    {
        foreach (var command in new[] { StartCommand, StopCommand, PauseResumeCommand })
        {
            (command as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private static string FormatTime(double ms)
    {
        var time = TimeSpan.FromMilliseconds(Math.Max(0, ms));
        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";
    }
}
