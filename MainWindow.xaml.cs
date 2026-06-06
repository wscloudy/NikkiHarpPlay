using System.Windows;
using MidiKeyPlayer.Services;
using MidiKeyPlayer.ViewModels;

namespace MidiKeyPlayer;

public partial class MainWindow : Window
{
    private readonly HotkeyService _hotkeyService = new();
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hotkeyService.StopRequested += _viewModel.StopFromHotkey;
        _hotkeyService.Register(this);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _hotkeyService.StopRequested -= _viewModel.StopFromHotkey;
        _hotkeyService.Dispose();
    }
}
