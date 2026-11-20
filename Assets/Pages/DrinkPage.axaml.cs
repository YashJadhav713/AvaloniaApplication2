using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaApplication2.Services;

namespace AvaloniaApplication2.Assets.Pages
{
    public partial class DrinkPage : UserControl
    {
        private const string VendiBiteApiUrl =
            "https://vmm.vendibite.com/machinetoserver.php";

        private static readonly HttpClient VendiBiteHttpClient =
            new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };

        private static readonly HttpClient _httpClient =
            new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(90)
            };

        private bool _apiRequestsStarted = false;

        public event Action? DrinkCompleted;
        public event Action<string>? DrinkFailed;

        private double _totalDurationSeconds = 85.0;
        private const int TotalStages = 7;
        private double _stageDurationSeconds = 12.0;

        private DispatcherTimer? _animationTimer;
        private DispatcherTimer? _stageTimer;
        private DateTime _animationStartTime;

        private int _currentStage = 0;
        private bool _finished = false;

        private CancellationTokenSource? _statusCts;
        private bool _readyReceived = false;
        private bool _navigationTriggered = false;

        private DateTime? _b20ScanStartTime;
        private const double B20TotalScanSeconds = 10.0;

        private const double CupTop = 178;
        private const double CupBottom = 618;
        private const double CupMaxHeight = 440;

        // ============================================================
        // STAGE COLOURS (theme)
        //   waiting  = muted grey
        //   current  = bright lime
        //   complete = white
        // ============================================================

        private readonly IBrush _waitingBrush =
            new SolidColorBrush(Color.Parse("#707070"));

        private readonly IBrush _currentBrush =
            new SolidColorBrush(Color.Parse("#FFFFFF"));

        private readonly IBrush _completeBrush =
            new SolidColorBrush(Color.Parse("#00A844"));

        private readonly string[] _stageNames =
        {
            "CHILL",
            "CUP",
            "BASE",
            "PROTEIN",
            "FLAVOUR",
            "BLEND",
            "DISPENSE"
        };


        // ============================================================
        // CONSTRUCTOR
        // ============================================================

        public DrinkPage()
        {
            InitializeComponent();

            if (CurrentOrder.DrinkId <= 0)
            {
                CurrentOrder.SetOrder(Drink.First, "Water");
            }

            DrinkData? selectedDrink =
                GetDrinkById(CurrentOrder.DrinkId);

            if (selectedDrink == null)
            {
                selectedDrink = Drink.First;

                CurrentOrder.SetOrder(
                    selectedDrink,
                    CurrentOrder.Base);
            }

            OrderIdText.Text = CurrentOrder.OrderId;
            DrinkNameText.Text = selectedDrink.Name;

            BuildingText.Text = selectedDrink.IsBar
                ? $"Dispensing your {selectedDrink.Name}"
                : $"Building your {selectedDrink.Name}";

            BaseText.Text = selectedDrink.IsBar
                ? "-"
                : CurrentOrder.Base.ToUpperInvariant();


            // --------------------------------------------------------
            // STEP 1: Verify & Reduce stock with safe fallback navigation.
            // --------------------------------------------------------

            bool milkSelected =
                string.Equals(
                    CurrentOrder.Base,
                    "Milk",
                    StringComparison.OrdinalIgnoreCase);

            bool stockOk = true;

            if (milkSelected && Drinks.GetMilkStock() <= 0)
            {
                stockOk = false;
            }
            else if (!Drinks.ReduceStock(CurrentOrder.DrinkId))
            {
                stockOk = false;
            }
            else if (milkSelected && !Drinks.ReduceMilkStock())
            {
                stockOk = false;
            }

            if (!stockOk)
            {
                BuildingText.Text = "OUT OF STOCK - RETURNING...";
                CurrentStageText.Text = "UNAVAILABLE";
                StatusText.Text = "Selected item is out of stock.";
                EtaText.Text = "RETURNING";

                DispatcherTimer exitTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2.0)
                };
                exitTimer.Tick += (s, e) =>
                {
                    exitTimer.Stop();
                    DrinkFailed?.Invoke("The selected drink ingredients are currently out of stock.");
                };
                exitTimer.Start();
                return;
            }


            // --------------------------------------------------------
            // STEP 3: Initial preparation timing
            // --------------------------------------------------------

            InitializePreparationTiming(selectedDrink);

            // --------------------------------------------------------
            // STEP 4: Reset UI animation
            // --------------------------------------------------------

            ResetAnimation();

            // --------------------------------------------------------
            // STEP 5: Send API updates & start preparation sequence
            // Place Order first, then updated Stock.
            // --------------------------------------------------------

            _ = SendVendiBiteUpdatesAsync(selectedDrink);
            _ = StartPreparationSequenceAsync();
        }


        // ============================================================
        // PREPARATION TIMING CALCULATION
        // Glass (6s) + Stepper/Powder (16s) + Liquid/Blender (37s) + Actuator Rev 1 (8s) + Door/Rev 2 (25s)
        // ============================================================

        private void InitializePreparationTiming(DrinkData selectedDrink)
        {
            if (selectedDrink.IsBar)
            {
                _totalDurationSeconds = 16.0;
            }
            else
            {
                double powderTime = 9.0;
                double waterTime = 30.0;
                bool hasLiquid = !string.Equals(CurrentOrder.Base, "None", StringComparison.OrdinalIgnoreCase);

                double powderStepTime = Math.Max(15.0, powderTime + 6.0);
                double liquidStepTime = hasLiquid ? Math.Max(20.0, waterTime + 7.0) : 20.0;
                _totalDurationSeconds = 6.0 + powderStepTime + liquidStepTime + 8.0 + 25.0; // ~92s
            }

            _stageDurationSeconds = _totalDurationSeconds / TotalStages;
            Console.WriteLine($"[PREPARATION] Initial estimated time: {_totalDurationSeconds:F1}s for {selectedDrink.Name} (Base: {CurrentOrder.Base})");
        }

        // ============================================================
        // PREPARATION SEQUENCE (/sendbits then /drinktime)
        // 1. Send the order, then call /drinktime once. It returns T90.
        // 2. Start a 90 s countdown on the screen.
        // 3. Do not call /getbits for the first 95% of the time (about 85s for T90).
        // 4. From then on, call /getbits once every 1 second, not faster.
        //    Make one call at a time, and start the next only after the last one replies.
        // 5. React to the bit:
        //    B20: stop the countdown and show "Please take your drink".
        //    B21: show "Thank you".
        //    B10: the machine is idle, so allow the next order.
        // 6. If the countdown reaches 0 and B20 has not come yet:
        //    keep showing "Almost ready..." and keep calling /getbits every second.
        //    Do not fail and do not move on. Only the bits decide the next step.
        // ============================================================

        private async Task StartPreparationSequenceAsync()
        {
            _statusCts = new CancellationTokenSource();
            CancellationToken token = _statusCts.Token;

            try
            {
                // Send the 15-bit recipe packet to controller
                await SendDrinkBits();

                // If this is a protein bar order, no glass dispensing or drink timer is needed.
                // Jump directly to pickup completion.
                DrinkData? selectedDrink = GetDrinkById(CurrentOrder.DrinkId);
                if (selectedDrink != null && selectedDrink.IsBar)
                {
                    Console.WriteLine("[PREPARATION] Protein bar order detected. Advancing directly to pickup.");
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        DrinkCompleted?.Invoke();
                    });
                    return;
                }

                // Call /drinktime once to get drink time and start countdown
                await CallDrinkTimeAndStartCountdownAsync(token);

                // Start /getbits monitoring after first 95% of time has elapsed
                _ = StartStatusMonitoring(token);
            }
            catch (OperationCanceledException)
            {
                // Normal exit when navigating away
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PREPARATION SEQUENCE ERROR] {ex.Message}");
            }
        }

        private async Task CallDrinkTimeAndStartCountdownAsync(CancellationToken token)
        {
            int attempts = 0;
            const int maxAttempts = 3;

            while (attempts < maxAttempts && !token.IsCancellationRequested && !_navigationTriggered)
            {
                attempts++;
                Console.WriteLine($"[DRINKTIME] Calling /drinktime (Attempt {attempts}/{maxAttempts})...");

                string? responseText = null;
                try
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

                    HttpResponseMessage response =
                        await _httpClient.GetAsync($"{MachineBits.BaseUrl}/drinktime", linkedCts.Token);

                    responseText = (await response.Content.ReadAsStringAsync())?.Trim();
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DRINKTIME ERROR] HTTP error: {ex.Message}");
                }

                Console.WriteLine($"[DRINKTIME] Controller response: {responseText}");

                if (!string.IsNullOrEmpty(responseText))
                {
                    // 1. T<seconds> (e.g. T90) -> Start countdown on screen
                    if (responseText.StartsWith("T", StringComparison.OrdinalIgnoreCase))
                    {
                        string numPart = responseText.Substring(1).Trim('"', ' ');
                        if (int.TryParse(numPart, out int drinkSeconds) && drinkSeconds > 0)
                        {
                            Console.WriteLine($"[DRINKTIME] Received T{drinkSeconds}. Starting {drinkSeconds}s countdown.");

                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                _totalDurationSeconds = drinkSeconds;
                                _stageDurationSeconds = _totalDurationSeconds / TotalStages;
                                StartAnimation();
                            });

                            return;
                        }
                    }

                    // 2. B3 -> Glass failed, no drink will be made -> show sorry page
                    if (responseText.Contains(MachineBits.StatusGlassFailed))
                    {
                        Console.WriteLine("[DRINKTIME] Glass failed (B3). Aborting to sorry page.");
                        await Dispatcher.UIThread.InvokeAsync(HandleGlassFailed);
                        return;
                    }

                    // 3. INVALID -> No order running -> show sorry page
                    if (responseText.Contains("INVALID", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("[DRINKTIME] INVALID order response. Aborting to sorry page.");
                        await Dispatcher.UIThread.InvokeAsync(() => HandleMachineError("No active order was recognized by the controller."));
                        return;
                    }

                    // 4. B6 -> Glass result not ready yet (after 4s)
                    if (responseText.Contains(MachineBits.StatusBusy))
                    {
                        Console.WriteLine($"[DRINKTIME] Glass result not ready yet (B6) (Attempt {attempts}/{maxAttempts})");
                        if (attempts >= maxAttempts)
                        {
                            Console.WriteLine("[DRINKTIME] Reached maximum 3 attempts for B6. Aborting to sorry page.");
                            await Dispatcher.UIThread.InvokeAsync(() => HandleMachineError("The machine cup detection sensor timed out."));
                            return;
                        }

                        await Task.Delay(2000, token);
                        continue;
                    }
                }

                if (attempts >= maxAttempts)
                {
                    Console.WriteLine("[DRINKTIME] Max attempts reached with no valid reply. Starting default countdown.");
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        StartAnimation();
                    });
                    return;
                }

                await Task.Delay(1000, token);
            }
        }


        // ============================================================
        // VENDIBITE API FLOW
        // PLACE ORDER FIRST, THEN UPDATE STOCK
        // ============================================================

        private async Task SendVendiBiteUpdatesAsync(
            DrinkData selectedDrink)
        {
            if (_apiRequestsStarted)
                return;

            _apiRequestsStarted = true;

            try
            {
                string machineId =
                    Drinks.MachineId?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(machineId))
                    return;

                // PLACE ORDER API
                await SendPlaceOrderAsync(
                    selectedDrink,
                    machineId);

                // STOCK API
                // Reads the latest locally saved stock values.
                await SendStockUpdateAsync(machineId);
            }
            catch
            {
                // API failures must not interrupt drink preparation.
            }
        }


        // ============================================================
        // PLACE ORDER API
        // ============================================================

        private async Task SendPlaceOrderAsync(
            DrinkData selectedDrink,
            string machineId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CurrentOrder.OrderId))
                {
                    string? orderId = await VendiBiteService.PlaceOrderAsync();
                    if (!string.IsNullOrWhiteSpace(orderId))
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            OrderIdText.Text = orderId;
                        });
                    }
                }
            }
            catch
            {
                // Silent failure. Continue preparation.
            }
        }


        // ============================================================
        // SEND DRINK BITS TO FLASK CONTROLLER (15 BITS)
        // ============================================================

        private async Task SendDrinkBits()
        {
            await MachineBits.SendDrinkBitsAsync(_httpClient, CurrentOrder.DrinkId, CurrentOrder.Base);
        }


        // =========================================================
        // MACHINE STATUS MONITORING (/getbits)
        // 1. Do not call /getbits for the first 95% of the time.
        //    For T90, start calling at about 85 s.
        // 2. From then on, call /getbits once every 1 second, not faster.
        //    Make one call at a time, and start next only after last one replies.
        // 3. React to the bit:
        //    B20: stop the countdown and show "Please take your drink".
        //    B21: show "Thank you".
        //    B10: the machine is idle, so allow the next order.
        // =========================================================

        private async Task StartStatusMonitoring(CancellationToken token)
        {
            try
            {
                // Start polling once every 1 second near completion (90% of time)
                double delaySeconds = Math.Max(1.0, _totalDurationSeconds * 0.90);
                if (delaySeconds < 1.0)
                    delaySeconds = 1.0;

                Console.WriteLine($"[STATUS] Waiting {delaySeconds:F1}s before starting /getbits polling...");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token);

                Console.WriteLine("[STATUS] Starting /getbits polling: once every 1 second...");

                while (!token.IsCancellationRequested && !_navigationTriggered)
                {
                    try
                    {
                        using var requestCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, requestCts.Token);

                        HttpResponseMessage response =
                            await _httpClient.GetAsync(
                                $"{MachineBits.BaseUrl}/getbits",
                                linkedCts.Token);

                        if (response.IsSuccessStatusCode)
                        {
                            string result =
                                await response.Content.ReadAsStringAsync();

                            Console.WriteLine(
                                $"[GET BITS] {result}");

                            // =================================================
                            // B3 = STATUS_GLASS_FAILED
                            // =================================================
                            if (result.Contains(MachineBits.StatusGlassFailed))
                            {
                                Console.WriteLine("[STATUS] Glass Dispense Failed (B3)");
                                await Dispatcher.UIThread.InvokeAsync(HandleGlassFailed);
                                return;
                            }

                            // =================================================
                            // B7 = STATUS_POWDER_FAILED
                            // =================================================
                            if (result.Contains(MachineBits.StatusPowderFailed))
                            {
                                Console.WriteLine("[STATUS] Powder Dispense Failed (B7)");
                                await Dispatcher.UIThread.InvokeAsync(HandlePowderFailed);
                                return;
                            }

                            // =================================================
                            // B5 = STATUS_ERROR
                            // =================================================
                            if (result.Contains(MachineBits.StatusError))
                            {
                                Console.WriteLine("[STATUS] Machine Error (B5)");
                                await Dispatcher.UIThread.InvokeAsync(() => HandleMachineError("A machine mechanism error occurred during preparation."));
                                return;
                            }

                            // =================================================
                            // B20 = STATUS_PICKUP_WAIT (Pickup LED blinking, drink ready)
                            // Stop countdown and show "Please take your drink"
                            // =================================================
                            if (result.Contains(MachineBits.StatusPickupWait) ||
                                result.Contains(MachineBits.StatusReady) ||
                                result.Contains(MachineBits.StatusPickedUp) ||
                                result.Contains(MachineBits.StatusPickup) ||
                                result.Contains(MachineBits.LegacyReady))
                            {
                                Console.WriteLine($"[STATUS] Drink ready bit received ({result.Trim()}) -> B20");
                                _readyReceived = true;

                                await Dispatcher.UIThread.InvokeAsync(
                                    HandleDrinkReady);

                                return;
                            }
                        }
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        // HTTP request timeout waiting for status change. Continue listening.
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[GET BITS ERROR] {ex.Message}");
                    }

                    // Call once every 1 second, not faster.
                    // Start next call ONLY after the last one replies.
                    await Task.Delay(1000, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal when DrinkPage is closed.
            }
        }


        // =========================================================
        // ERROR HANDLERS (GLASS, POWDER, MACHINE ERRORS)
        // =========================================================

        private void HandleGlassFailed()
        {
            if (_navigationTriggered)
                return;

            _navigationTriggered = true;

            _stageTimer?.Stop();
            _stageTimer = null;

            _animationTimer?.Stop();
            _animationTimer = null;

            _statusCts?.Cancel();
            _statusCts?.Dispose();
            _statusCts = null;

            BuildingText.Text = "GLASS DISPENSE FAILED";
            CurrentStageText.Text = "ERROR";
            StatusText.Text = "Could not dispense glass into holder.";
            EtaText.Text = "ABORTED";

            DispatcherTimer exitTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2.0)
            };
            exitTimer.Tick += (s, e) =>
            {
                exitTimer.Stop();
                DrinkFailed?.Invoke("The machine was unable to position a cup in the dispensing chamber.");
            };
            exitTimer.Start();
        }

        private void HandlePowderFailed()
        {
            if (_navigationTriggered)
                return;

            _navigationTriggered = true;

            _stageTimer?.Stop();
            _stageTimer = null;

            _animationTimer?.Stop();
            _animationTimer = null;

            _statusCts?.Cancel();
            _statusCts?.Dispose();
            _statusCts = null;

            BuildingText.Text = "INGREDIENT FEED FAILED";
            CurrentStageText.Text = "ERROR";
            StatusText.Text = "Protein powder could not be dispensed into glass.";
            EtaText.Text = "ABORTED";

            DispatcherTimer exitTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2.0)
            };
            exitTimer.Tick += (s, e) =>
            {
                exitTimer.Stop();
                DrinkFailed?.Invoke("The protein powder ingredients could not be filled into the cup.");
            };
            exitTimer.Start();
        }

        private void HandleMachineError(string reason)
        {
            if (_navigationTriggered)
                return;

            _navigationTriggered = true;

            _stageTimer?.Stop();
            _stageTimer = null;

            _animationTimer?.Stop();
            _animationTimer = null;

            _statusCts?.Cancel();
            _statusCts?.Dispose();
            _statusCts = null;

            BuildingText.Text = "MACHINE INTERRUPTION";
            CurrentStageText.Text = "ERROR";
            StatusText.Text = reason;
            EtaText.Text = "ABORTED";

            DispatcherTimer exitTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2.0)
            };
            exitTimer.Tick += (s, e) =>
            {
                exitTimer.Stop();
                DrinkFailed?.Invoke(reason);
            };
            exitTimer.Start();
        }


        // =========================================================
        // DRINK READY (B12 / B20)
        // =========================================================

        private void HandleDrinkReady()
        {
            if (_readyReceived == false)
                return;

            if (_navigationTriggered)
                return;

            // =====================================================
            // READY TRIGGER
            // =====================================================

            _navigationTriggered = true;

            // =====================================================
            // STOP MONITORING
            // =====================================================

            _statusCts?.Cancel();
            _statusCts?.Dispose();
            _statusCts = null;

            // =====================================================
            // FINISH ANIMATION AT 100%
            // =====================================================

            FinishAnimation();

            // Smooth 0.5s pause so user sees 100% READY before moving to pickup page
            DispatcherTimer transitionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            transitionTimer.Tick += (s, e) =>
            {
                transitionTimer.Stop();
                DrinkCompleted?.Invoke();
            };
            transitionTimer.Start();
        }


        // ============================================================
        // UPDATE STOCK API
        // 9 NAME / QUANTITY PAIRS
        // DRINKS 1-8 + MILK
        // ============================================================

        private async Task SendStockUpdateAsync(string machineId)
        {
            try
            {
                await VendiBiteService.UpdateStockAsync();
            }
            catch
            {
                // Silent failure. Continue preparation.
            }
        }


        // ============================================================
        // GET DRINK / BAR BY FIXED ID
        // ============================================================

        private static DrinkData? GetDrinkById(int drinkId)
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


        // ============================================================
        // RESET ANIMATION
        // ============================================================

        private void ResetAnimation()
        {
            _currentStage = 0;
            _finished = false;

            CompletionText.Text = "0%";
            SchemaProgress.Text = "0%";
            EtaText.Text = $"ETA  -  {(int)Math.Ceiling(_totalDurationSeconds)}S";
            CurrentStageText.Text = "WAITING";
            StatusText.Text = "Preparing your drink...";
            LiquidLevelText.Text = "0%";

            ProgressBar.Width = 0;

            CupLiquid.Height = 0;
            Canvas.SetTop(CupLiquid, CupBottom);

            LiquidHighlight.Height = 0;
            Canvas.SetTop(LiquidHighlight, CupBottom);

            CompletePanel.IsVisible = false;

            ResetStage(StageText1, StageLine1);
            ResetStage(StageText2, StageLine2);
            ResetStage(StageText3, StageLine3);
            ResetStage(StageText4, StageLine4);
            ResetStage(StageText5, StageLine5);
            ResetStage(StageText6, StageLine6);
            ResetStage(StageText7, StageLine7);
        }


        private void ResetStage(TextBlock text, Border line)
        {
            text.Foreground = _waitingBrush;
            line.Background = _waitingBrush;
        }


        // ============================================================
        // START ANIMATION
        // ============================================================

        private void StartAnimation()
        {
            _animationStartTime = DateTime.Now;

            _animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };

            _animationTimer.Tick += AnimationTimer_Tick;
            _stageTimer = _animationTimer;
            _animationTimer.Start();
        }


        // ============================================================
        // ANIMATION TIMER
        // ============================================================

        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            if (_finished)
                return;

            double elapsed =
                (DateTime.Now - _animationStartTime).TotalSeconds;

            if (_readyReceived)
            {
                FinishAnimation();
                return;
            }

            // Progress smoothly along calculated duration
            double progress = elapsed / _totalDurationSeconds;

            // DONT jump to next page until sensors send the bit!
            // If ready bit hasn't arrived, cap progress at 98% and keep finalizing animation
            if (progress >= 0.98)
                progress = 0.98;

            int stage =
                (int)Math.Floor(elapsed / _stageDurationSeconds) + 1;

            if (stage > TotalStages)
                stage = TotalStages;

            _currentStage = stage;

            UpdateAnimation(progress, elapsed);
        }


        // ============================================================
        // UPDATE ANIMATION
        // ============================================================

        private void UpdateAnimation(double progress, double elapsed)
        {
            int percentage = (int)Math.Round(progress * 100);

            CompletionText.Text = $"{percentage}%";
            SchemaProgress.Text = $"{percentage}%";

            ProgressBar.Width = 1000 * progress;

            double liquidHeight = CupMaxHeight * progress;
            CupLiquid.Height = liquidHeight;

            Canvas.SetTop(CupLiquid, CupBottom - liquidHeight);

            double highlightHeight = liquidHeight * 0.88;
            LiquidHighlight.Height = highlightHeight;

            Canvas.SetTop(
                LiquidHighlight,
                CupBottom - highlightHeight - 8);

            LiquidLevelText.Text = $"{percentage}%";

            if (_currentStage >= 1 &&
                _currentStage <= TotalStages)
            {
                CurrentStageText.Text =
                    _stageNames[_currentStage - 1];

                StatusText.Text =
                    GetStageStatus(_currentStage);
            }

            UpdateAllStages();

            int remaining =
                (int)Math.Ceiling(_totalDurationSeconds - elapsed);

            if (remaining <= 0)
                remaining = 0;

            // ============================================================
            // B20 SCANNING LOGIC:
            // 1. If B20 not received in 5s: scan for 5 more seconds.
            // 2. Still not received after 10s: go to next page after 10s of countdown,
            //    but DO NOT show this countdown on the page (keep showing "Almost ready...").
            // 3. If B20 arrives at ANY time: go to next page immediately.
            // ============================================================
            if (remaining == 0 && !_readyReceived)
            {
                if (_b20ScanStartTime == null)
                {
                    _b20ScanStartTime = DateTime.UtcNow;
                    Console.WriteLine("[B20 SCAN] Countdown reached 0. Starting Phase 1: 5s scan for B20 bit...");
                }

                double b20Elapsed = (DateTime.UtcNow - _b20ScanStartTime.Value).TotalSeconds;

                CurrentStageText.Text = "FINALIZING";
                StatusText.Text = "Almost ready...";
                EtaText.Text = "Almost ready...";

                if (b20Elapsed >= B20TotalScanSeconds && !_navigationTriggered)
                {
                    Console.WriteLine("[B20 SCAN] B20 bit not received after 10 seconds of scanning (5s + 5s). Advancing to next page after silent 10s countdown.");
                    _readyReceived = true;
                    HandleDrinkReady();
                }
            }
            else if (remaining > 0)
            {
                EtaText.Text = $"ETA  -  {remaining}S";
            }
        }


        // ============================================================
        // STAGE STATUS
        // ============================================================

        private string GetStageStatus(int stage)
        {
            DrinkData? selectedDrink = GetDrinkById(CurrentOrder.DrinkId);
            bool isBar = selectedDrink != null && selectedDrink.IsBar;

            if (isBar)
            {
                return stage switch
                {
                    1 => "Initializing dispenser mechanism...",
                    2 => "Locating protein bar slot...",
                    3 => "Unlocking delivery gate...",
                    4 => "Dispensing your protein bar...",
                    5 => "Verifying bar drop...",
                    6 => "Clearing dispensing channel...",
                    7 => "Bar ready for pickup...",
                    _ => "Dispensing your bar..."
                };
            }

            bool noBase = string.Equals(
                CurrentOrder.Base,
                "None",
                StringComparison.OrdinalIgnoreCase);

            return stage switch
            {
                1 => "Cooling the preparation chamber...",
                2 => "Positioning the drink cup...",
                3 => noBase
                    ? "No base needed..."
                    : $"Adding {CurrentOrder.Base.ToLowerInvariant()}...",
                4 => "Adding protein...",
                5 => "Adding flavour...",
                6 => "Blending your drink...",
                7 => "Preparing drink for pickup...",
                _ => "Preparing your drink..."
            };
        }


        // ============================================================
        // UPDATE ALL STAGES
        // ============================================================

        private void UpdateAllStages()
        {
            UpdateStage(1, StageText1, StageLine1);
            UpdateStage(2, StageText2, StageLine2);
            UpdateStage(3, StageText3, StageLine3);
            UpdateStage(4, StageText4, StageLine4);
            UpdateStage(5, StageText5, StageLine5);
            UpdateStage(6, StageText6, StageLine6);
            UpdateStage(7, StageText7, StageLine7);
        }


        private void UpdateStage(
            int stage,
            TextBlock text,
            Border line)
        {
            if (_currentStage < stage)
            {
                text.Foreground = _waitingBrush;
                line.Background = _waitingBrush;
                return;
            }

            if (_currentStage == stage)
            {
                text.Foreground = _currentBrush;
                line.Background = _currentBrush;
                return;
            }

            text.Foreground = _completeBrush;
            line.Background = _completeBrush;
        }


        // ============================================================
        // FINISH
        // ============================================================

        private void FinishAnimation()
        {
            if (_finished)
                return;

            _finished = true;

            _animationTimer?.Stop();
            _animationTimer = null;
            _stageTimer = null;

            CompletionText.Text = "100%";
            SchemaProgress.Text = "100%";
            LiquidLevelText.Text = "100%";

            CurrentStageText.Text = "READY";
            StatusText.Text = "Your drink is ready.";
            EtaText.Text = "ETA  -  READY";

            ProgressBar.Width = 1000;

            CupLiquid.Height = CupMaxHeight;
            Canvas.SetTop(CupLiquid, CupBottom - CupMaxHeight);

            LiquidHighlight.Height = CupMaxHeight * 0.88;

            Canvas.SetTop(
                LiquidHighlight,
                CupBottom - (CupMaxHeight * 0.88) - 8);

            _currentStage = TotalStages;
            UpdateAllStages();

            CompletePanel.IsVisible = true;
        }


        // ============================================================
        // CLEANUP
        // ============================================================

        protected override void OnDetachedFromVisualTree(
            Avalonia.VisualTreeAttachmentEventArgs e)
        {
            _animationTimer?.Stop();
            _animationTimer = null;

            _stageTimer?.Stop();
            _stageTimer = null;

            _statusCts?.Cancel();
            _statusCts?.Dispose();
            _statusCts = null;

            base.OnDetachedFromVisualTree(e);
        }
    }
}