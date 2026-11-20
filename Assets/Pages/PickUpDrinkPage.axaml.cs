using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AvaloniaApplication2.Services;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaApplication2.Assets.Pages;

public partial class PickUpDrinkPage : UserControl
{
    public event Action? CompletedRequested;
    public event Action? HomeRequested;

    private DispatcherTimer? _pickupStepTimer;
    private int _pickupSecondsElapsed = 0;
    private const int B21Phase1Seconds = 5;
    private const int B21Phase2Seconds = 5;
    private const int B21CountdownSeconds = 5;

    private static readonly HttpClient _httpClient =
        new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };


    // =========================================================
    // ANIMATION
    // =========================================================

    private CancellationTokenSource? _animationCancellation;


    // =========================================================
    // CONDITIONS
    // =========================================================

    private bool _animationFinished = false;

    private bool _pickupReceived = false;

    private bool _completedTriggered = false;


    // =========================================================
    // CUP / DRINK ANIMATION
    // =========================================================

    private const double CupNormalScale = 1.0;

    private const double CupBreathScale = 1.03;

    private const int CupBreathDurationMilliseconds = 700;

    private const int CupAnimationTotalMilliseconds = 1200;


    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    public PickUpDrinkPage()
    {
        InitializeComponent();

        AttachedToVisualTree +=
            PickUpDrinkPage_AttachedToVisualTree;

        DetachedFromVisualTree +=
            PickUpDrinkPage_DetachedFromVisualTree;
    }


    // =========================================================
    // PAGE ATTACHED
    // =========================================================

    private async void PickUpDrinkPage_AttachedToVisualTree(
        object? sender,
        Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _animationCancellation?.Cancel();

        _animationCancellation?.Dispose();

        _animationCancellation =
            new CancellationTokenSource();

        CancellationToken token =
            _animationCancellation.Token;

        _animationFinished = false;
        _pickupReceived = false;
        _completedTriggered = false;


        // =====================================================
        // START STATUS / PICKUP MONITORING (B21)
        // =====================================================

        StartPickupMonitoring();


        try
        {
            ResetPage();

            // =================================================
            // SET TEXT (DRINK / BAR)
            // =================================================

            ApplyItemTexts();


            // =================================================
            // LOAD SELECTED DRINK / BAR IMAGE
            // =================================================

            LoadDrinkImage();


            // =================================================
            // READY LABEL
            // =================================================

            await FadeIn(
                ReadyLabel,
                300,
                token);


            // =================================================
            // PICKUP TEXT
            // =================================================

            await Task.Delay(
                50,
                token);


            await FadeIn(
                PickUpText,
                400,
                token);


            // =================================================
            // SUB TEXT
            // =================================================

            await Task.Delay(
                50,
                token);


            await FadeIn(
                PickUpSubText,
                300,
                token);


            // =================================================
            // DRINK IMAGE
            // =================================================

            await Task.Delay(
                50,
                token);


            await FadeIn(
                CupAnimation,
                350,
                token);


            // =================================================
            // DRINK IMAGE BREATHING
            // =================================================

            await AnimateCupForFourSeconds(
                token);


            // =================================================
            // PICKUP STATUS
            // =================================================

            await FadeIn(
                PickupStatusText,
                300,
                token);


            // =================================================
            // INFORMATION PANEL
            // =================================================

            await Task.Delay(
                50,
                token);


            await FadeIn(
                InfoPanel,
                300,
                token);


            // =================================================
            // ANIMATION FINISHED
            // Wait until customer picks up drink (sensor bit B21/B10)
            // =================================================

            _animationFinished = true;

            CheckCompletion();
        }
        catch (OperationCanceledException)
        {
        }
    }


    // =========================================================
    // APPLY TEXTS : "DRINK" FOR DRINKS, "BAR" FOR PROTEIN BARS
    // =========================================================

    private void ApplyItemTexts()
    {
        DrinkData? selectedDrink =
            CurrentOrder.Drink ?? GetDrinkById(CurrentOrder.DrinkId);

        string item =
            selectedDrink != null && selectedDrink.IsBar
                ? "BAR"
                : "DRINK";

        ReadyLabel.Text =
            $"YOUR {item} IS READY";

        PickUpText.Text =
            $"PICK UP YOUR {item}";

        PickUpSubText.Text =
            $"PLEASE COLLECT YOUR {item}";

        YourItemText.Text =
            $"YOUR {item}";

        InfoTitleText.Text =
            $"YOUR {item} IS READY";

        InfoSubText.Text =
            $"PLEASE TAKE YOUR {item}";

        if (!string.IsNullOrWhiteSpace(CurrentOrder.OrderId))
        {
            OrderIdText.Text = CurrentOrder.OrderId;
            InfoSubText.Text = $"PLEASE TAKE YOUR {item}  |  ORDER #{CurrentOrder.OrderId}";
        }
    }


    // =========================================================
    // LOAD DRINK / BAR IMAGE
    // =========================================================

    private void LoadDrinkImage()
    {
        DrinkData? selectedDrink =
            CurrentOrder.Drink ?? GetDrinkById(CurrentOrder.DrinkId);

        if (selectedDrink == null)
            return;

        try
        {
            DrinkImage.Source = DrinkImageService.LoadDrinkBitmap(selectedDrink);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Drink image error: {ex.Message}");
        }
    }


    // =========================================================
    // GET DRINK / BAR BY ID
    // =========================================================

    private static DrinkData? GetDrinkById(
        int drinkId)
    {
        return drinkId switch
        {
            1 => Drink.First,
            2 => Drink.Second,
            3 => Drink.Third,
            4 => Drink.Fourth,
            5 => Drink.Fifth,
            6 => Drink.Sixth,
            7 => Drink.Seventh,
            8 => Drink.Eighth,
            9 => Bar.First,
            10 => Bar.Second,
            _ => null
        };
    }


    // =========================================================
    // STATUS / PICKUP MONITORING (/getbits) - BIT B21
    // 1. Scan for B21 bit for 5 seconds. If received -> go to next page.
    // 2. If not received B21 in 5s: try again for 5 seconds.
    // 3. Still not received B21 after 10s: start 5-second countdown on page, then go to next page.
    // 4. If B21 is received at ANY time: go to next page immediately!
    // =========================================================

    private void StartPickupMonitoring()
    {
        _pickupStepTimer?.Stop();
        _pickupSecondsElapsed = 0;

        if (BarCountdownPanel != null)
        {
            BarCountdownPanel.IsVisible = false;
        }

        _pickupStepTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _pickupStepTimer.Tick += async (s, e) =>
        {
            if (_completedTriggered)
            {
                _pickupStepTimer?.Stop();
                _pickupStepTimer = null;
                return;
            }

            // 1. Check bits from machine per second
            try
            {
                using var requestCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    _animationCancellation?.Token ?? CancellationToken.None,
                    requestCts.Token);

                HttpResponseMessage response = await _httpClient.GetAsync(
                    $"{MachineBits.BaseUrl}/getbits",
                    linkedCts.Token);

                if (response.IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[PICKUP GET BITS] {result}");

                    // Bit B21 = StatusPickedUp / StatusPickup
                    if (result.Contains(MachineBits.StatusPickedUp) ||
                        result.Contains(MachineBits.StatusPickup))
                    {
                        Console.WriteLine("[PICKUP] Sensor bit B21 received! Advancing to next page immediately.");
                        _pickupReceived = true;
                        AdvanceToNextPage();
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal on page navigation
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PICKUP GET BITS ERROR] {ex.Message}");
            }

            // 2. Increment elapsed scanning seconds
            _pickupSecondsElapsed++;

            DrinkData? currentItem =
                CurrentOrder.Drink ?? (CurrentOrder.DrinkId == 9 ? Bar.First : CurrentOrder.DrinkId == 10 ? Bar.Second : null);
            bool isBar = (currentItem != null && currentItem.IsBar) ||
                         CurrentOrder.DrinkId == 9 ||
                         CurrentOrder.DrinkId == 10;

            if (_pickupSecondsElapsed <= B21Phase1Seconds)
            {
                // Phase 1: First 5 seconds of scanning (hidden countdown)
                if (BarCountdownPanel != null)
                    BarCountdownPanel.IsVisible = false;
            }
            else if (_pickupSecondsElapsed <= (B21Phase1Seconds + B21Phase2Seconds))
            {
                // Phase 2: Next 5 seconds ("try again for 5 seconds", hidden countdown)
                if (_pickupSecondsElapsed == B21Phase1Seconds + 1)
                {
                    Console.WriteLine("[PICKUP] B21 bit not received in first 5s. Scanning for 5 more seconds...");
                }

                if (BarCountdownPanel != null)
                    BarCountdownPanel.IsVisible = false;
            }
            else
            {
                // Phase 3: Still not received B21 after 10s (5s + 5s) -> start the 5-second countdown on page
                int totalLimit = B21Phase1Seconds + B21Phase2Seconds + B21CountdownSeconds; // 15 seconds
                int countdownRemaining = totalLimit - _pickupSecondsElapsed;
                if (countdownRemaining < 0)
                    countdownRemaining = 0;

                if (BarCountdownPanel != null)
                {
                    BarCountdownPanel.IsVisible = true;
                }

                if (BarCountdownText != null)
                {
                    BarCountdownText.Text = isBar
                        ? $"RETURNING TO HOME IN {countdownRemaining}s"
                        : $"RETURNING IN {countdownRemaining}s";
                }

                Console.WriteLine($"[PICKUP] 5s countdown active: {countdownRemaining}s remaining...");

                if (countdownRemaining <= 0)
                {
                    Console.WriteLine("[PICKUP] 5s countdown complete without B21. Advancing to next page.");
                    AdvanceToNextPage();
                }
            }
        };

        _pickupStepTimer.Start();
    }


    // =========================================================
    // CHECK BOTH CONDITIONS
    // =========================================================

    private void CheckCompletion()
    {
        if (_completedTriggered)
            return;

        if (!_animationFinished)
            return;

        if (!_pickupReceived)
            return;

        AdvanceToNextPage();
    }


    // =========================================================
    // ADVANCE TO NEXT PAGE (CompletedPage for Drinks, HomePage for Bars)
    // =========================================================

    private void AdvanceToNextPage()
    {
        if (_completedTriggered)
            return;

        _completedTriggered = true;

        _pickupStepTimer?.Stop();
        _pickupStepTimer = null;

        _animationCancellation?.Cancel();

        DrinkData? currentItem =
            CurrentOrder.Drink ?? (CurrentOrder.DrinkId == 9 ? Bar.First : CurrentOrder.DrinkId == 10 ? Bar.Second : null);

        bool isBarOrder = (currentItem != null && currentItem.IsBar) ||
                         CurrentOrder.DrinkId == 9 ||
                         CurrentOrder.DrinkId == 10;

        if (isBarOrder)
        {
            Console.WriteLine("[PICKUP] Bar order complete: Advancing directly to Home Page.");
            HomeRequested?.Invoke();
        }
        else
        {
            Console.WriteLine("[PICKUP] Drink order complete: Advancing to Completed Page.");
            CompletedRequested?.Invoke();
        }
    }


    // =========================================================
    // RESET
    // =========================================================

    private void ResetPage()
    {
        ReadyLabel.Opacity = 0;

        PickUpText.Opacity = 0;

        PickUpSubText.Opacity = 0;

        CupAnimation.Opacity = 0;

        PickupStatusText.Opacity = 0;

        InfoPanel.Opacity = 0;

        if (BarCountdownPanel != null)
        {
            BarCountdownPanel.IsVisible = false;
        }

        SetCupScale(
            CupNormalScale);
    }


    // =========================================================
    // FADE IN
    // =========================================================

    private static async Task FadeIn(
        Control control,
        int durationMilliseconds,
        CancellationToken token)
    {
        const int steps = 30;


        int delay =
            Math.Max(
                1,
                durationMilliseconds / steps);


        for (int i = 0; i <= steps; i++)
        {
            token.ThrowIfCancellationRequested();


            double opacity =
                (double)i / steps;


            control.Opacity =
                opacity;


            await Task.Delay(
                delay,
                token);
        }


        control.Opacity = 1;
    }


    // =========================================================
    // DRINK IMAGE BREATHING
    // =========================================================

    private async Task AnimateCupForFourSeconds(
        CancellationToken token)
    {
        DateTime startTime =
            DateTime.UtcNow;


        bool growing = true;


        while (
            (DateTime.UtcNow - startTime).TotalMilliseconds
            < CupAnimationTotalMilliseconds)
        {
            token.ThrowIfCancellationRequested();


            double from =
                growing
                    ? CupNormalScale
                    : CupBreathScale;


            double to =
                growing
                    ? CupBreathScale
                    : CupNormalScale;


            await AnimateCupScale(
                from,
                to,
                CupBreathDurationMilliseconds,
                token);


            growing = !growing;
        }


        // =====================================================
        // FINAL STATE
        // =====================================================

        SetCupScale(
            CupNormalScale);
    }


    // =========================================================
    // CUP SCALE
    // =========================================================

    private async Task AnimateCupScale(
        double from,
        double to,
        int durationMilliseconds,
        CancellationToken token)
    {
        const int steps = 20;


        int delay =
            Math.Max(
                1,
                durationMilliseconds / steps);


        for (int i = 0; i <= steps; i++)
        {
            token.ThrowIfCancellationRequested();


            double progress =
                (double)i / steps;


            double scale =
                from +
                ((to - from) * progress);


            SetCupScale(
                scale);


            await Task.Delay(
                delay,
                token);
        }
    }


    // =========================================================
    // SET CUP SCALE
    // =========================================================

    private void SetCupScale(
        double scaleValue)
    {
        if (CupAnimation.RenderTransform
            is not ScaleTransform scale)
        {
            return;
        }


        scale.ScaleX =
            scaleValue;


        scale.ScaleY =
            scaleValue;
    }


    // =========================================================
    // DETACHED
    // =========================================================

    private void PickUpDrinkPage_DetachedFromVisualTree(
        object? sender,
        Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _pickupStepTimer?.Stop();
        _pickupStepTimer = null;

        _animationCancellation?.Cancel();

        _animationCancellation?.Dispose();

        _animationCancellation = null;
    }
}
