using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AvaloniaApplication2.Services;
using System;
using System.Threading.Tasks;
using ZXing;
using ZXing.Common;

namespace AvaloniaApplication2.Assets.Pages
{
    public partial class PaymentPage : UserControl
    {
        public event Action? DrinkRequested;
        public event Action? HomeRequested;

        private readonly PhonePeService _phonePeService;

        private string _merchantId = "";
        private string _transactionId = "";

        private DispatcherTimer? _statusTimer;
        private DispatcherTimer? _drinkTimer;
        private DispatcherTimer? _homeTimer;
        private DispatcherTimer? _countdownTimer;

        private DispatcherTimer? _animationTimer;
        private DispatcherTimer? _accentTimer;

        private bool _paymentFinished;
        private int _paymentSecondsLeft;

        private int _animationStep;


        public PaymentPage()
        {
            InitializeComponent();

            _phonePeService = new PhonePeService();

            LoadCurrentOrder();

            StartVisualAnimation();

            if (Drinks.PaymentEnabled)
            {
                StartPaymentMode();
            }
            else
            {
                StartFreeMode();
            }
        }


        // ============================================================
        // LOAD ORDER
        // ============================================================

        private void LoadCurrentOrder()
        {
            OrderDrinkName.Text = CurrentOrder.DrinkName;

            if (CurrentOrder.Base == "Milk")
            {
                OrderBase.Text = "Cold milk";
            }
            else
            {
                OrderBase.Text = "Chilled water";
            }

            UpdateOrderIdDisplay();

            TotalDueText.Text = $"INR {CurrentOrder.Price}";
        }

        private void UpdateOrderIdDisplay()
        {
            string orderId = CurrentOrder.OrderId;
            if (!string.IsNullOrWhiteSpace(orderId))
            {
                OrderIdText.Text = orderId;
                OrderIdDisplay.Text = orderId;
                FreeModeOrderIdText.Text = $"ORDER #{orderId}";
            }
            else
            {
                OrderIdText.Text = "";
                OrderIdDisplay.Text = "WAITING...";
                FreeModeOrderIdText.Text = "";

                _ = Task.Run(async () =>
                {
                    string? fetchedId = await VendiBiteService.PlaceOrderAsync();
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (!string.IsNullOrWhiteSpace(fetchedId))
                        {
                            OrderIdText.Text = fetchedId;
                            OrderIdDisplay.Text = fetchedId;
                            FreeModeOrderIdText.Text = $"ORDER #{fetchedId}";
                        }
                        else
                        {
                            OrderIdDisplay.Text = "";
                        }
                    });
                });
            }
        }


        // ============================================================
        // FREE MODE
        // ============================================================

        private void StartFreeMode()
        {
            StopAllTimers();

            PaymentView.IsVisible = false;

            FreeModeView.IsVisible = true;

            _drinkTimer =
                new DispatcherTimer
                {
                    Interval =
                        TimeSpan.FromSeconds(2)
                };

            _drinkTimer.Tick +=
                FreeModeTimer_Tick;

            _drinkTimer.Start();
        }


        private void FreeModeTimer_Tick(
            object? sender,
            EventArgs e)
        {
            _drinkTimer?.Stop();

            _drinkTimer = null;

            DrinkRequested?.Invoke();
        }


        // ============================================================
        // PAYMENT MODE
        // ============================================================

        private async void StartPaymentMode()
        {
            StopAllTimers();

            PaymentView.IsVisible = true;
            FreeModeView.IsVisible = false;

            _paymentFinished = false;

            _merchantId = "";
            _transactionId = "";

            WaitingPanel.IsVisible = true;

            ReceivedPanel.IsVisible = false;
            FailedPanel.IsVisible = false;

            PaymentReceivedCircle.IsVisible = false;
            PaymentFailedCircle.IsVisible = false;

            QrCard.IsVisible = true;
            QrContainer.IsVisible = true;

            QrImage.IsVisible = false;
            QrPlaceholder.IsVisible = true;

            QrPlaceholder.Text =
                "CREATING QR";

            PaymentCountdownText.Text =
                "PAYMENT TIME LEFT 180 SEC";

            PaymentCountdownText.IsVisible =
                true;

            CountdownCard.IsVisible = true;

            OrangeAccent.IsVisible = true;
            CancelOrderButton.IsVisible = true;
            StartVisualAnimation();


            // --------------------------------------------------------
            // READ MID
            // --------------------------------------------------------

            _merchantId =
                Drinks.MerchantId?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(
                    _merchantId))
            {
                ShowPaymentFailed(
                    "MID IS NOT CONFIGURED.");

                return;
            }


            // --------------------------------------------------------
            // PAYMENT AMOUNT
            // --------------------------------------------------------

            decimal amount;

            try
            {
                amount =
                    Convert.ToDecimal(
                        CurrentOrder.Price);
            }
            catch
            {
                ShowPaymentFailed(
                    "INVALID PAYMENT AMOUNT.");

                return;
            }

            if (amount <= 0)
            {
                ShowPaymentFailed(
                    "INVALID PAYMENT AMOUNT.");

                return;
            }


            // --------------------------------------------------------
            // CREATE PHONEPE PAYMENT
            // --------------------------------------------------------

            try
            {
                PhonePeService.PhonePeResponse result =
                    await _phonePeService.CreatePaymentAsync(
                        _merchantId,
                        amount);

                if (!result.Success)
                {
                    ShowPaymentFailed(
                        string.IsNullOrWhiteSpace(
                            result.Message)
                            ? "PAYMENT COULD NOT BE CREATED."
                            : result.Message);

                    return;
                }


                // ----------------------------------------------------
                // TRANSACTION ID
                // ----------------------------------------------------

                _transactionId =
                    result.TransactionId;


                // ----------------------------------------------------
                // QR STRING
                // ----------------------------------------------------

                if (string.IsNullOrWhiteSpace(
                    result.QrString))
                {
                    ShowPaymentFailed(
                        "QR CODE NOT RECEIVED.");

                    return;
                }


                // ----------------------------------------------------
                // SHOW QR
                // ----------------------------------------------------

                ShowQrCode(
                    result.QrString);


                // ----------------------------------------------------
                // START COUNTDOWN
                // ----------------------------------------------------

                StartPaymentCountdown();


                // ----------------------------------------------------
                // START STATUS CHECK
                // ----------------------------------------------------

                StartPaymentStatusChecking();
            }
            catch (Exception ex)
            {
                ShowPaymentFailed(
                    ex.Message);
            }
        }


        // ============================================================
        // QR IMAGE
        // ============================================================

        private void ShowQrCode(
            string qrString)
        {
            try
            {
                BarcodeWriterPixelData writer =
                    new BarcodeWriterPixelData
                    {
                        Format =
                            BarcodeFormat.QR_CODE,

                        Options =
                            new EncodingOptions
                            {
                                Width = 375,
                                Height = 375,
                                Margin = 0
                            }
                    };


                var pixelData =
                    writer.Write(qrString);


                var bitmap =
                    new WriteableBitmap(
                        new PixelSize(
                            pixelData.Width,
                            pixelData.Height),

                        new Vector(
                            96,
                            96),

                        Avalonia.Platform.PixelFormat.Bgra8888);


                using (var frame = bitmap.Lock())
                {
                    int rowBytes = pixelData.Width * 4;
                    if (frame.RowBytes == rowBytes)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(
                            pixelData.Pixels,
                            0,
                            frame.Address,
                            pixelData.Pixels.Length);
                    }
                    else
                    {
                        for (int y = 0; y < pixelData.Height; y++)
                        {
                            nint destRow = frame.Address + (y * frame.RowBytes);
                            System.Runtime.InteropServices.Marshal.Copy(
                                pixelData.Pixels,
                                y * rowBytes,
                                destRow,
                                rowBytes);
                        }
                    }
                }


                QrImage.Source =
                    bitmap;

                QrPlaceholder.IsVisible =
                    false;

                QrImage.IsVisible =
                    true;

                StartQrAnimation();
            }
            catch
            {
                ShowPaymentFailed(
                    "UNABLE TO DISPLAY QR CODE.");
            }
        }


        // ============================================================
        // PAYMENT COUNTDOWN
        // ============================================================

        private void StartPaymentCountdown()
        {
            _countdownTimer?.Stop();

            _paymentSecondsLeft = 180;

            UpdatePaymentCountdown();

            _countdownTimer =
                new DispatcherTimer
                {
                    Interval =
                        TimeSpan.FromSeconds(1)
                };

            _countdownTimer.Tick +=
                PaymentCountdownTimer_Tick;

            _countdownTimer.Start();
        }


        private void PaymentCountdownTimer_Tick(
            object? sender,
            EventArgs e)
        {
            if (_paymentFinished)
            {
                _countdownTimer?.Stop();

                return;
            }

            _paymentSecondsLeft--;

            if (_paymentSecondsLeft <= 0)
            {
                _paymentSecondsLeft = 0;

                UpdatePaymentCountdown();

                _countdownTimer?.Stop();

                _countdownTimer = null;

                ShowPaymentFailed(
                    "PAYMENT TIME EXPIRED.");

                return;
            }

            UpdatePaymentCountdown();
        }


        private void UpdatePaymentCountdown()
        {
            PaymentCountdownText.Text =
                $"PAYMENT TIME LEFT {_paymentSecondsLeft} SEC";

            if (_paymentSecondsLeft <= 30)
            {
                PaymentCountdownText.Foreground =
                    new SolidColorBrush(
                        Color.Parse("#FF3B30"));
            }
            else
            {
                PaymentCountdownText.Foreground =
                    new SolidColorBrush(
                        Color.Parse("#00A844"));
            }
        }


        // ============================================================
        // PHONEPE STATUS CHECK
        // ============================================================

        private void StartPaymentStatusChecking()
        {
            _statusTimer?.Stop();

            _statusTimer =
                new DispatcherTimer
                {
                    Interval =
                        TimeSpan.FromSeconds(4)
                };

            _statusTimer.Tick +=
                PaymentStatusTimer_Tick;

            _statusTimer.Start();
        }


        private async void PaymentStatusTimer_Tick(
            object? sender,
            EventArgs e)
        {
            if (_paymentFinished)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(
                _transactionId))
            {
                return;
            }

            try
            {
                PhonePeService.PhonePeResponse result =
                    await _phonePeService.CheckPaymentStatusAsync(
                        _merchantId,
                        _transactionId);


                // ----------------------------------------------------
                // SUCCESS
                // ----------------------------------------------------

                if (result.Code ==
                    "PAYMENT_SUCCESS")
                {
                    ShowPaymentSuccess(
                        result.TransactionId);

                    return;
                }


                // ----------------------------------------------------
                // FAILED
                // ----------------------------------------------------

                if (result.Code ==
                        "PAYMENT_FAILED"
                    ||
                    result.Code ==
                        "PAYMENT_CANCELLED"
                    ||
                    result.Code ==
                        "PAYMENT_ERROR"
                    ||
                    result.Code ==
                        "AUTHORIZATION_FAILED"
                    ||
                    result.Code ==
                        "PAYMENT_DECLINED"
                    ||
                    result.Code ==
                        "TRANSACTION_NOT_FOUND")
                {
                    ShowPaymentFailed(
                        string.IsNullOrWhiteSpace(
                            result.Message)
                            ? "PAYMENT FAILED."
                            : result.Message);

                    return;
                }

                // Pending = keep checking.
            }
            catch
            {
                // Network error = keep checking.
            }
        }


        // ============================================================
        // PAYMENT SUCCESS
        // ============================================================

        private void ShowPaymentSuccess(
            string transactionId)
        {
            if (_paymentFinished)
            {
                return;
            }

            _paymentFinished = true;

            _statusTimer?.Stop();
            _statusTimer = null;

            _countdownTimer?.Stop();
            _countdownTimer = null;

            WaitingPanel.IsVisible = false;

            QrCard.IsVisible = false;
            CountdownCard.IsVisible = false;
            OrangeAccent.IsVisible = false;
            CancelOrderButton.IsVisible = false;

            FailedPanel.IsVisible = false;
            PaymentFailedCircle.IsVisible = false;

            ReceivedPanel.IsVisible = true;
            PaymentReceivedCircle.IsVisible = true;

            PaymentCountdownText.IsVisible = false;

            TransactionIdText.Text =
                transactionId;

            StartSuccessAnimation();


            _drinkTimer?.Stop();

            _drinkTimer =
                new DispatcherTimer
                {
                    Interval =
                        TimeSpan.FromSeconds(3)
                };

            _drinkTimer.Tick +=
                DrinkTimer_Tick;

            _drinkTimer.Start();
        }


        // ============================================================
        // PAYMENT FAILED
        // ============================================================

        private void ShowPaymentFailed(
            string message)
        {
            if (_paymentFinished)
            {
                return;
            }

            _paymentFinished = true;

            _statusTimer?.Stop();
            _statusTimer = null;

            _countdownTimer?.Stop();
            _countdownTimer = null;

            WaitingPanel.IsVisible = false;

            QrCard.IsVisible = false;
            CountdownCard.IsVisible = false;
            OrangeAccent.IsVisible = false;
            CancelOrderButton.IsVisible = false;

            ReceivedPanel.IsVisible = false;
            PaymentReceivedCircle.IsVisible = false;

            PaymentFailedCircle.IsVisible = true;

            PaymentCountdownText.IsVisible = false;

            FailedPanel.IsVisible = true;

            FailedMessageText.Text =
                string.IsNullOrWhiteSpace(message)
                    ? "PAYMENT FAILED"
                    : message.ToUpperInvariant();

            StartFailureAnimation();


            _homeTimer?.Stop();

            _homeTimer =
                new DispatcherTimer
                {
                    Interval =
                        TimeSpan.FromSeconds(3)
                };

            _homeTimer.Tick +=
                HomeTimer_Tick;

            _homeTimer.Start();
        }


        // ============================================================
        // SUCCESS -> DRINK
        // ============================================================

        private void DrinkTimer_Tick(
            object? sender,
            EventArgs e)
        {
            _drinkTimer?.Stop();

            _drinkTimer = null;

            DrinkRequested?.Invoke();
        }


        // ============================================================
        // FAILURE -> HOME
        // ============================================================

        private void HomeTimer_Tick(
            object? sender,
            EventArgs e)
        {
            _homeTimer?.Stop();

            _homeTimer = null;

            HomeRequested?.Invoke();
        }


        // ============================================================
        // QR ANIMATION
        // ============================================================

        private void StartQrAnimation()
        {
            _animationStep = 0;

            _animationTimer?.Stop();

            _animationTimer =
                new DispatcherTimer
                {
                    Interval =
                        TimeSpan.FromMilliseconds(40)
                };

            _animationTimer.Tick +=
                QrAnimationTimer_Tick;

            _animationTimer.Start();
        }


        private void QrAnimationTimer_Tick(
            object? sender,
            EventArgs e)
        {
            _animationStep++;

            double scale;

            if (_animationStep < 8)
            {
                scale =
                    0.92 +
                    (_animationStep * 0.01);
            }
            else
            {
                scale = 1.0;

                _animationTimer?.Stop();
            }

            QrImage.RenderTransform =
                new ScaleTransform(
                    scale,
                    scale);
        }


        // ============================================================
        // SUCCESS ANIMATION
        // ============================================================

        private void StartSuccessAnimation()
        {
            _animationStep = 0;

            _animationTimer?.Stop();

            _animationTimer =
                new DispatcherTimer
                {
                    Interval =
                        TimeSpan.FromMilliseconds(40)
                };

            _animationTimer.Tick +=
                SuccessAnimationTimer_Tick;

            _animationTimer.Start();
        }


        private void SuccessAnimationTimer_Tick(
            object? sender,
            EventArgs e)
        {
            _animationStep++;

            double scale =
                0.85 +
                (_animationStep * 0.025);

            if (scale >= 1.0)
            {
                scale = 1.0;

                _animationTimer?.Stop();
            }

            PaymentReceivedCircle.RenderTransform =
                new ScaleTransform(
                    scale,
                    scale);
        }


        // ============================================================
        // FAILURE ANIMATION
        // ============================================================

        private void StartFailureAnimation()
        {
            _animationStep = 0;

            _animationTimer?.Stop();

            _animationTimer =
                new DispatcherTimer
                {
                    Interval =
                        TimeSpan.FromMilliseconds(50)
                };

            _animationTimer.Tick +=
                FailureAnimationTimer_Tick;

            _animationTimer.Start();
        }


        private void FailureAnimationTimer_Tick(
            object? sender,
            EventArgs e)
        {
            _animationStep++;

            double scale;

            if (_animationStep <= 5)
            {
                scale =
                    0.85 +
                    (_animationStep * 0.03);
            }
            else
            {
                scale = 1.0;

                _animationTimer?.Stop();
            }

            PaymentFailedCircle.RenderTransform =
                new ScaleTransform(
                    scale,
                    scale);
        }


        // ============================================================
        // VISUAL ANIMATION
        // ============================================================

        private void StartVisualAnimation()
        {
            _accentTimer?.Stop();

            _accentTimer =
                new DispatcherTimer
                {
                    Interval =
                        TimeSpan.FromMilliseconds(450)
                };

            int step = 0;
            _accentTimer.Tick += (s, e) =>
            {
                step++;
                OrangeAccent.Opacity = (step % 2 == 0) ? 1.0 : 0.6;
            };

            _accentTimer.Start();
        }

        // ============================================================
        // CANCEL ORDER
        // ============================================================

        private void CancelOrderButton_Click(
            object? sender,
            Avalonia.Interactivity.RoutedEventArgs e)
        {
            _paymentFinished = true;
            StopAllTimers();
            _accentTimer?.Stop();
            _accentTimer = null;
            HomeRequested?.Invoke();
        }

        // ============================================================
        // STOP ALL TIMERS
        // ============================================================

        private void StopAllTimers()
        {
            _statusTimer?.Stop();
            _drinkTimer?.Stop();
            _homeTimer?.Stop();
            _countdownTimer?.Stop();
            _animationTimer?.Stop();

            _statusTimer = null;
            _drinkTimer = null;
            _homeTimer = null;
            _countdownTimer = null;
            _animationTimer = null;
        }


        // ============================================================
        // CLEANUP
        // ============================================================

        protected override void OnDetachedFromVisualTree(
            VisualTreeAttachmentEventArgs e)
        {
            StopAllTimers();
            _accentTimer?.Stop();
            _accentTimer = null;

            base.OnDetachedFromVisualTree(e);
        }
    }
}