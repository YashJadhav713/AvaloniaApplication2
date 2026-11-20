using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;

namespace AvaloniaApplication2.Assets.Pages;

public partial class ErrorPage : UserControl
{
    public event Action? HomeRequested;

    private DispatcherTimer? _countdownTimer;
    private int _secondsRemaining = 15;
    private bool _navigated = false;

    public ErrorPage()
    {
        InitializeComponent();

        AttachedToVisualTree += ErrorPage_AttachedToVisualTree;
        DetachedFromVisualTree += ErrorPage_DetachedFromVisualTree;

        PointerPressed += (s, e) => NavigateHome();
    }

    public void SetErrorInfo(string? errorMessage, string? contactNumber = null)
    {
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            ErrorMessageText.Text = errorMessage;
        }

        string phone = !string.IsNullOrWhiteSpace(contactNumber)
            ? contactNumber
            : Drinks.ServiceContactNumber;

        ContactNumberText.Text = phone;

        string machine = string.IsNullOrWhiteSpace(Drinks.MachineId)
            ? "NUTRIPOD-01"
            : Drinks.MachineId;

        MachineDetailsText.Text = $"MACHINE ID: {machine}";
    }

    private void ErrorPage_AttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _secondsRemaining = 15;
        _navigated = false;
        UpdateCountdownText();

        _countdownTimer?.Stop();
        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += CountdownTimer_Tick;
        _countdownTimer.Start();
    }

    private void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        _secondsRemaining--;

        if (_secondsRemaining <= 0)
        {
            _countdownTimer?.Stop();
            NavigateHome();
            return;
        }

        UpdateCountdownText();
    }

    private void UpdateCountdownText()
    {
        ReturnButton.Content = $"RETURN TO HOME ({_secondsRemaining}S)";
    }

    private void ReturnButton_Click(object? sender, RoutedEventArgs e)
    {
        NavigateHome();
    }

    private void NavigateHome()
    {
        if (_navigated)
            return;

        _navigated = true;

        _countdownTimer?.Stop();
        _countdownTimer = null;

        HomeRequested?.Invoke();
    }

    private void ErrorPage_DetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _countdownTimer?.Stop();
        _countdownTimer = null;
    }
}
