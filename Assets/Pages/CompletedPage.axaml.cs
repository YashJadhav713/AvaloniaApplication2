using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AvaloniaApplication2.Services;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaApplication2.Assets.Pages;

public partial class CompletedPage : UserControl
{
    // =========================================================
    // EVENT
    // =========================================================

    public event Action? HomeRequested;

    private DispatcherTimer? _b10StepTimer;
    private int _b10SecondsElapsed = 0;
    private const int B10Phase1Seconds = 10;
    private const int B10Phase2Seconds = 10;
    private const int B10TotalSeconds = 10;


    // =========================================================
    // HTTP CLIENT
    // =========================================================

    private static readonly HttpClient _httpClient =
        new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(90)
        };


    // =========================================================
    // ANIMATION
    // =========================================================

    private CancellationTokenSource? _animationCancellation;


    // =========================================================
    // CONDITIONS
    // =========================================================

    private bool _animationFinished = false;

    private bool _b10Received = false;

    private bool _homeTriggered = false;


    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    public CompletedPage()
    {
        InitializeComponent();

        AttachedToVisualTree +=
            CompletedPage_AttachedToVisualTree;

        DetachedFromVisualTree +=
            CompletedPage_DetachedFromVisualTree;
    }


    // =========================================================
    // PAGE ATTACHED
    // =========================================================

    private async void CompletedPage_AttachedToVisualTree(
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

        _b10Received = false;

        _homeTriggered = false;

        // =====================================================
        // START B10 MONITORING (10s scan + 10s scan -> Home)
        // =====================================================

        StartB10Monitoring();


        try
        {
            ResetPage();


            // =================================================
            // LOAD CURRENT DRINK NAME
            // =================================================

            DrinkData? currentItem =
                CurrentOrder.Drink ?? GetDrinkById(CurrentOrder.DrinkId);

            YourItemText.Text =
                currentItem != null && currentItem.IsBar
                    ? "YOUR BAR"
                    : "YOUR DRINK";

            DrinkText.Text =
                currentItem != null && currentItem.IsBar
                    ? CurrentOrder.DrinkName
                    : $"{CurrentOrder.DrinkName} Drink";


            // =================================================
            // LOAD DRINK / BAR IMAGE
            // =================================================

            LoadDrinkImage();


            // =================================================
            // THANK YOU
            // =================================================

            DeliveredText.Text = "THANK YOU";

            await FadeIn(
                DeliveredText,
                600,
                token);


            // =================================================
            // GLASS
            // =================================================

            await Task.Delay(
                150,
                token);

            await FadeIn(
                GlassImage,
                800,
                token);


            // =================================================
            // ENJOY YOUR
            // =================================================

            await Task.Delay(
                150,
                token);

            await FadeIn(
                EnjoyText,
                700,
                token);


            // =================================================
            // DRINK NAME
            // =================================================

            await Task.Delay(
                100,
                token);

            await FadeIn(
                DrinkText,
                700,
                token);


            // =================================================
            // THANK YOU
            // =================================================

            await Task.Delay(
                150,
                token);

            await FadeIn(
                ThankYouText,
                700,
                token);


            // =================================================
            // ANIMATION FINISHED
            // =================================================

            _animationFinished = true;

            CheckHomeCondition();
        }
        catch (OperationCanceledException)
        {
        }
    }


    // =========================================================
    // STATUS / B10 MONITORING (/getbits) - BIT B10
    // 1. Scan for B10 bit for 5 seconds. If received -> go to Home page.
    // 2. If not received in 5s: try again for 5 seconds.
    // 3. Still not received B10 after 10s: go to Home page.
    // 4. If B10 is received at ANY time: go to Home page immediately!
    // =========================================================

    private void StartB10Monitoring()
    {
        _b10StepTimer?.Stop();
        _b10SecondsElapsed = 0;

        _b10StepTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _b10StepTimer.Tick += async (s, e) =>
        {
            if (_homeTriggered)
            {
                _b10StepTimer?.Stop();
                _b10StepTimer = null;
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
                    Console.WriteLine($"[COMPLETED GET BITS] {result}");

                    // Bit B10 = StatusIdle
                    if (result.Contains(MachineBits.StatusIdle))
                    {
                        Console.WriteLine("[COMPLETED] Sensor bit B10 received! Advancing to main Home page.");
                        _b10Received = true;
                        CheckHomeCondition();
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Page closed
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[COMPLETED GET BITS ERROR] {ex.Message}");
            }

            // 2. Increment elapsed seconds
            _b10SecondsElapsed++;

            if (_b10SecondsElapsed <= B10Phase1Seconds)
            {
                // Phase 1: First 5 seconds of scanning
            }
            else if (_b10SecondsElapsed <= (B10Phase1Seconds + B10Phase2Seconds))
            {
                // Phase 2: Next 5 seconds ("try again for 5 seconds")
                if (_b10SecondsElapsed == B10Phase1Seconds + 1)
                {
                    Console.WriteLine("[COMPLETED] B10 bit not received in first 5s. Trying again for 5 seconds...");
                }
            }
            else
            {
                // Phase 3: Still not received B10 after 10s (5s + 5s) -> go to main Home page
                Console.WriteLine("[COMPLETED] B10 not received after 10s (5s + 5s). Advancing to main Home page.");
                _b10Received = true;
                _animationFinished = true;
                CheckHomeCondition();
            }
        };

        _b10StepTimer.Start();
    }


    // =========================================================
    // CHECK BOTH CONDITIONS
    // =========================================================

    private void CheckHomeCondition()
    {
        if (_homeTriggered)
            return;


        // =====================================================
        // ANIMATION MUST BE FINISHED
        // =====================================================

        if (!_animationFinished)
            return;


        // =====================================================
        // B10 MUST BE RECEIVED
        // =====================================================

        if (!_b10Received)
            return;


        // =====================================================
        // TRIGGER ONLY ONCE
        // =====================================================

        _homeTriggered = true;

        _b10StepTimer?.Stop();
        _b10StepTimer = null;

        // =====================================================
        // STOP B10 MONITORING
        // =====================================================

        _animationCancellation?.Cancel();


        // =====================================================
        // GO TO HOME PAGE
        // =====================================================

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            HomeRequested?.Invoke();
        });
    }


    // =========================================================
    // RESET
    // =========================================================

    private void ResetPage()
    {
        DeliveredText.Opacity = 0;

        GlassImage.Opacity = 0;

        EnjoyText.Opacity = 0;

        DrinkText.Opacity = 0;

        ThankYouText.Opacity = 0;
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
            Console.WriteLine($"Completed page image error: {ex.Message}");
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
    // FADE IN
    // =========================================================

    private static async Task FadeIn(
        Control control,
        int durationMilliseconds,
        CancellationToken token)
    {
        const int steps = 40;

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
    // DETACHED
    // =========================================================

    private void CompletedPage_DetachedFromVisualTree(
        object? sender,
        Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _b10StepTimer?.Stop();
        _b10StepTimer = null;

        _animationCancellation?.Cancel();

        _animationCancellation?.Dispose();

        _animationCancellation = null;
    }
}
