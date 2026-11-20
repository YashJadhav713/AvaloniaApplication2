using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using AvaloniaApplication2.Services;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaApplication2.Assets.Pages;

public partial class HomePage : UserControl
{
    private static readonly HttpClient _doorHttpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

    private CancellationTokenSource? _doorPollingCancellation;
    private CancellationTokenSource? _animationCancellation;

    public event Action? OrderRequested;
    public event Action? AdminRequested;

    // ADMIN TAP SETTINGS
    private int _adminTapCount = 0;
    private DateTime _adminFirstTapTime = DateTime.MinValue;

    private const int AdminRequiredTaps = 6;
    private const double AdminTapWindowSeconds = 3.0;

    public HomePage()
    {
        InitializeComponent();

        CurrentOrder.Clear();

        // SET ORDER BUTTON IMAGE
        UpdateOrderButtonImage();

        // ORDER NOW CLICK
        OrderNowButton.Click += OrderNowButton_Click;

        // PAGE ATTACH / DETACH
        AttachedToVisualTree += HomePage_AttachedToVisualTree;
        DetachedFromVisualTree += HomePage_DetachedFromVisualTree;

        // ENTER = UNLOCK, ESC = CANCEL
        AdminPasswordBox.KeyDown += AdminPasswordBox_KeyDown;

        // ACCEPT DIGITS ONLY, MAXIMUM FOUR
        AdminPasswordBox.TextChanged += (_, _) =>
        {
            string text = AdminPasswordBox.Text ?? "";
            string digitsOnly = "";

            foreach (char c in text)
            {
                if (c >= '0' && c <= '9' &&
                    digitsOnly.Length < 4)
                {
                    digitsOnly += c;
                }
            }

            if (text != digitsOnly)
            {
                int caret = AdminPasswordBox.CaretIndex;

                AdminPasswordBox.Text = digitsOnly;

                AdminPasswordBox.CaretIndex =
                    Math.Min(caret, digitsOnly.Length);
            }

            // CLEAR ERROR STATE WHEN USER TYPES AGAIN
            if (digitsOnly.Length > 0)
            {
                SetPasswordErrorState(false);
            }
        };
    }

    // =========================================================
    // UPDATE ORDER BUTTON IMAGE BASED ON PAYMENT MODE
    // =========================================================

    private void UpdateOrderButtonImage()
    {
        string imagePath = Drinks.PaymentEnabled
            ? "/Assets/Image/OrderNowButton.png"
            : "/Assets/Image/FreeModeOrder.png";

        Uri assetUri = new Uri(
            $"avares://AvaloniaApplication2{imagePath}");

        using var stream = AssetLoader.Open(assetUri);

        OrderNowImage.Source = new Bitmap(stream);
    }

    // =========================================================
    // ORDER NOW
    // =========================================================

    private void OrderNowButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (DoorOpenOverlay.IsVisible)
        {
            return;
        }

        OrderRequested?.Invoke();
    }

    // =========================================================
    // PASSWORD BOX ERROR STATE (RED BORDER)
    // =========================================================

    private void SetPasswordErrorState(bool isError)
    {
        if (isError)
        {
            if (!AdminPasswordBox.Classes.Contains("error"))
                AdminPasswordBox.Classes.Add("error");
        }
        else
        {
            AdminPasswordBox.Classes.Remove("error");
            AdminPasswordError.Text = "";
        }
    }

    // =========================================================
    // KEYBOARD: ENTER / ESC
    // =========================================================

    private void AdminPasswordBox_KeyDown(
        object? sender,
        KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            UnlockAdminButton_Click(null, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelAdminPasswordButton_Click(null, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    // =========================================================
    // HIDDEN ADMIN: SIX TAPS WITHIN THREE SECONDS
    // =========================================================

    private void AdminTapButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (DoorOpenOverlay.IsVisible)
        {
            return;
        }

        DateTime now = DateTime.Now;

        if (_adminTapCount == 0 ||
            (now - _adminFirstTapTime).TotalSeconds >
            AdminTapWindowSeconds)
        {
            _adminTapCount = 1;
            _adminFirstTapTime = now;
            return;
        }

        _adminTapCount++;

        if (_adminTapCount >= AdminRequiredTaps)
        {
            _adminTapCount = 0;
            _adminFirstTapTime = DateTime.MinValue;

            AdminPasswordBox.Text = "";
            SetPasswordErrorState(false);

            AdminPasswordOverlay.IsVisible = true;
            AdminPasswordBox.Focus();
        }
    }

    // =========================================================
    // ON-SCREEN NUMERIC KEYPAD HANDLERS
    // =========================================================

    private void KeypadDigit_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string digit)
        {
            string current = AdminPasswordBox.Text ?? "";
            if (current.Length < 4)
            {
                AdminPasswordBox.Text = current + digit;
                SetPasswordErrorState(false);
                AdminPasswordError.Text = "";

                if (AdminPasswordBox.Text.Length == 4)
                {
                    TryUnlock();
                }
            }
        }
    }

    private void KeypadBackspace_Click(object? sender, RoutedEventArgs e)
    {
        string current = AdminPasswordBox.Text ?? "";
        if (current.Length > 0)
        {
            AdminPasswordBox.Text = current.Substring(0, current.Length - 1);
            SetPasswordErrorState(false);
            AdminPasswordError.Text = "";
        }
    }

    private void KeypadClear_Click(object? sender, RoutedEventArgs e)
    {
        AdminPasswordBox.Text = "";
        SetPasswordErrorState(false);
        AdminPasswordError.Text = "";
    }

    // =========================================================
    // UNLOCK ADMIN
    // =========================================================

    private void UnlockAdminButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        TryUnlock();
    }

    private void TryUnlock()
    {
        string enteredPassword =
            AdminPasswordBox.Text ?? "";

        if (enteredPassword.Length != 4)
        {
            SetPasswordErrorState(true);

            AdminPasswordError.Text =
                "Enter all 4 digits.";

            AdminPasswordBox.Focus();
            return;
        }

        if (enteredPassword != Drinks.AdminPassword)
        {
            AdminPasswordBox.Text = "";

            SetPasswordErrorState(true);

            AdminPasswordError.Text =
                "Incorrect password. Try again.";

            AdminPasswordBox.Focus();
            return;
        }

        AdminPasswordOverlay.IsVisible = false;
        AdminPasswordBox.Text = "";
        SetPasswordErrorState(false);

        AdminRequested?.Invoke();
    }

    // =========================================================
    // CANCEL PASSWORD PROMPT
    // =========================================================

    private void CancelAdminPasswordButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        AdminPasswordOverlay.IsVisible = false;

        AdminPasswordBox.Text = "";
        SetPasswordErrorState(false);
    }

    // =========================================================
    // HOME PAGE ATTACHED
    // =========================================================

    private async void HomePage_AttachedToVisualTree(
        object? sender,
        VisualTreeAttachmentEventArgs e)
    {
        // REFRESH IMAGE WHEN RETURNING TO HOME
        UpdateOrderButtonImage();

        // START REAL-TIME DOOR STATUS POLLING
        StartDoorPolling();

        _animationCancellation?.Cancel();
        _animationCancellation?.Dispose();

        _animationCancellation =
            new CancellationTokenSource();

        CancellationToken token =
            _animationCancellation.Token;

        try
        {
            await AnimateOrderButton(token);
        }
        catch (OperationCanceledException)
        {
            // ANIMATION CANCELLED WHEN PAGE DETACHES
        }
    }

    // =========================================================
    // ORDER BUTTON ANIMATION
    // =========================================================

    private async Task AnimateOrderButton(
        CancellationToken token)
    {
        const double normalScale = 1.0;
        const double maximumScale = 1.035;

        const int animationSteps = 10;
        const int stepDelay = 10;
        const int pauseDelay = 125;

        ScaleTransform scaleTransform =
            new ScaleTransform(normalScale, normalScale);

        OrderNowButton.RenderTransform = scaleTransform;

        OrderNowButton.RenderTransformOrigin =
            new RelativePoint(
                0.5,
                0.5,
                RelativeUnit.Relative);

        while (!token.IsCancellationRequested)
        {
            // SCALE UP
            for (int i = 0; i <= animationSteps; i++)
            {
                token.ThrowIfCancellationRequested();

                double progress =
                    (double)i / animationSteps;

                double scale = normalScale +
                    ((maximumScale - normalScale) * progress);

                scaleTransform.ScaleX = scale;
                scaleTransform.ScaleY = scale;

                await Task.Delay(stepDelay, token);
            }

            await Task.Delay(pauseDelay, token);

            // SCALE DOWN
            for (int i = animationSteps; i >= 0; i--)
            {
                token.ThrowIfCancellationRequested();

                double progress =
                    (double)i / animationSteps;

                double scale = normalScale +
                    ((maximumScale - normalScale) * progress);

                scaleTransform.ScaleX = scale;
                scaleTransform.ScaleY = scale;

                await Task.Delay(stepDelay, token);
            }

            await Task.Delay(pauseDelay, token);
        }
    }

    // =========================================================
    // HOME PAGE DETACHED
    // =========================================================

    private void HomePage_DetachedFromVisualTree(
        object? sender,
        VisualTreeAttachmentEventArgs e)
    {
        // STOP REAL-TIME DOOR POLLING IMMEDIATELY
        StopDoorPolling();

        _animationCancellation?.Cancel();
        _animationCancellation?.Dispose();

        _animationCancellation = null;

        if (OrderNowButton.RenderTransform
            is ScaleTransform scaleTransform)
        {
            scaleTransform.ScaleX = 1.0;
            scaleTransform.ScaleY = 1.0;
        }
    }

    // =========================================================
    // REAL-TIME DOOR STATUS POLLING
    // =========================================================

    private void StartDoorPolling()
    {
        StopDoorPolling();

        _doorPollingCancellation = new CancellationTokenSource();
        _ = PollDoorStatusAsync(_doorPollingCancellation.Token);
    }

    private void StopDoorPolling()
    {
        _doorPollingCancellation?.Cancel();
        _doorPollingCancellation?.Dispose();
        _doorPollingCancellation = null;
    }

    private async Task PollDoorStatusAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Set request timeout to approximately 2 seconds, linked with page cancellationToken
                using var requestTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    requestTimeoutCts.Token);

                string? status = await MachineBits.GetDoorStatusAsync(_doorHttpClient, linkedCts.Token);

                string cleanStatus = status?.Trim('"', '\'', ' ', '\r', '\n', '\t') ?? "";

                if (string.Equals(cleanStatus, MachineBits.DoorOpen, StringComparison.OrdinalIgnoreCase))
                {
                    Dispatcher.UIThread.Post(() => SetDoorPopupVisible(true));
                }
                else if (string.Equals(cleanStatus, MachineBits.DoorClose, StringComparison.OrdinalIgnoreCase))
                {
                    Dispatcher.UIThread.Post(() => SetDoorPopupVisible(false));
                }
                else
                {
                    // Unknown response or non-success status code (cleanStatus neither B14 nor B15)
                    // Requirement 6: Keep current popup state unchanged.
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // User navigated away from Home Page: terminate polling immediately
                break;
            }
            catch (Exception ex)
            {
                // Request failed, timed out, or machine disconnected:
                // Requirement 5: Keep current popup state unchanged. Never interpret failure as B14 or B15.
                Console.WriteLine($"[DOOR POLLING] Request error: {ex.Message}");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                // Poll every 1 second
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // User navigated away during delay: terminate polling immediately
                break;
            }
        }
    }

    private void SetDoorPopupVisible(bool visible)
    {
        if (DoorOpenOverlay.IsVisible != visible)
        {
            DoorOpenOverlay.IsVisible = visible;
            if (visible)
            {
                // Unfocus any active inputs while Door Open popup is visible
                Focus();
            }
        }
    }
}
