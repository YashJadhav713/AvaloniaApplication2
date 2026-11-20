using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;

namespace AvaloniaApplication2.Assets.Pages;

public partial class ChangePasswordPage : UserControl
{
    public event Action? SaveRequested;
    public event Action? CancelRequested;

    private static readonly IBrush SuccessBrush =
        Brush.Parse("#00A844");

    private static readonly IBrush ErrorBrush =
        Brush.Parse("#FF5252");

    private TextBox _activeBox;

    public ChangePasswordPage()
    {
        InitializeComponent();

        _activeBox = CurrentPasswordBox;

        CurrentPasswordBox.GotFocus += (s, e) => _activeBox = CurrentPasswordBox;
        NewPasswordBox.GotFocus += (s, e) => _activeBox = NewPasswordBox;
        ConfirmPasswordBox.GotFocus += (s, e) => _activeBox = ConfirmPasswordBox;

        // Allow only digits, with a maximum of 4.
        CurrentPasswordBox.TextChanged += (_, _) =>
            KeepDigitsOnly(CurrentPasswordBox);

        NewPasswordBox.TextChanged += (_, _) =>
            KeepDigitsOnly(NewPasswordBox);

        ConfirmPasswordBox.TextChanged += (_, _) =>
            KeepDigitsOnly(ConfirmPasswordBox);

        SaveButton.Click += (_, _) =>
            SaveRequested?.Invoke();

        CancelButton.Click += (_, _) =>
            CancelRequested?.Invoke();
    }

    private void KeypadDigit_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string digit)
        {
            string current = _activeBox.Text ?? "";
            if (current.Length < 4)
            {
                _activeBox.Text = current + digit;
            }
        }
    }

    private void KeypadBackspace_Click(object? sender, RoutedEventArgs e)
    {
        string current = _activeBox.Text ?? "";
        if (current.Length > 0)
        {
            _activeBox.Text = current.Substring(0, current.Length - 1);
        }
    }

    private void KeypadClear_Click(object? sender, RoutedEventArgs e)
    {
        _activeBox.Text = "";
    }

    private static void KeepDigitsOnly(TextBox textBox)
    {
        string text = textBox.Text ?? "";
        string digits = "";

        foreach (char c in text)
        {
            if (c >= '0' && c <= '9' && digits.Length < 4)
                digits += c;
        }

        if (text != digits)
        {
            int caret = textBox.CaretIndex;
            textBox.Text = digits;
            textBox.CaretIndex = Math.Min(caret, digits.Length);
        }
    }

    public string CurrentPassword =>
        CurrentPasswordBox.Text ?? "";

    public string NewPassword =>
        NewPasswordBox.Text ?? "";

    public string ConfirmPassword =>
        ConfirmPasswordBox.Text ?? "";

    public void ShowStatus(string message, bool success = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = success
            ? SuccessBrush
            : ErrorBrush;
    }

    public void ClearFields()
    {
        CurrentPasswordBox.Text = "";
        NewPasswordBox.Text = "";
        ConfirmPasswordBox.Text = "";
        StatusText.Text = "";
    }
}
