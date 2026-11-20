using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Threading.Tasks;

namespace AvaloniaApplication2.Assets.Pages
{
    public partial class PaymentSettingsPage : UserControl
    {
        public event Action? BackRequested;

        private bool _isSaving = false;
        private bool _isCaps = true;
        private readonly System.Collections.Generic.List<Button> _letterButtons = new();
        private Button? _capsBtn;


        // =========================================================
        // THEME COLOURS
        // =========================================================

        private static readonly IBrush LimeBrush =
            new SolidColorBrush(Color.Parse("#00A844"));

        private static readonly IBrush BlackBrush =
            new SolidColorBrush(Color.Parse("#0A0A0A"));

        private static readonly IBrush DarkLimeBrush =
            new SolidColorBrush(Color.Parse("#005C25"));

        private static readonly IBrush WhiteBrush =
            new SolidColorBrush(Color.Parse("#FFFFFF"));

        private static readonly IBrush ErrorBrush =
            new SolidColorBrush(Color.Parse("#FF5252"));


        public PaymentSettingsPage()
        {
            InitializeComponent();

            LoadSettings();

            PaymentToggleButton.Click +=
                PaymentToggleButton_Click;

            SaveButton.Click +=
                SaveButton_Click;

            CancelButton.Click +=
                CancelButton_Click;

            _capsBtn = this.FindControl<Button>("CapsButton");
            CollectLetterButtons(this.FindControl<StackPanel>("RowLetters1"));
            CollectLetterButtons(this.FindControl<StackPanel>("RowLetters2"));
            CollectLetterButtons(this.FindControl<StackPanel>("RowLetters3"));
            UpdateCapsVisual();
        }


        // =========================================================
        // LOAD SETTINGS
        // =========================================================

        private void LoadSettings()
        {
            UpdatePaymentUI(
                Drinks.PaymentEnabled);

            MidTextBox.Text =
                Drinks.MerchantId ?? "";

            StatusText.Text = "";
        }


        // =========================================================
        // UPDATE PAYMENT UI
        // ON  = lime fill, black text
        // OFF = black fill, dark-lime outline, white text
        // =========================================================

        private void UpdatePaymentUI(
            bool enabled)
        {
            PaymentToggleButton.Content =
                enabled ? "ON" : "OFF";

            if (enabled)
            {
                PaymentToggleButton.Background =
                    LimeBrush;

                PaymentToggleButton.BorderBrush =
                    LimeBrush;

                PaymentToggleButton.Foreground =
                    BlackBrush;
            }
            else
            {
                PaymentToggleButton.Background =
                    BlackBrush;

                PaymentToggleButton.BorderBrush =
                    DarkLimeBrush;

                PaymentToggleButton.Foreground =
                    WhiteBrush;
            }
        }


        // =========================================================
        // PAYMENT ON / OFF
        // =========================================================

        private void PaymentToggleButton_Click(
            object? sender,
            RoutedEventArgs e)
        {
            bool newState =
                !Drinks.PaymentEnabled;

            bool saved =
                Drinks.SetPaymentEnabled(
                    newState);

            if (saved)
            {
                UpdatePaymentUI(
                    newState);

                StatusText.Text =
                    "PAYMENT SETTING SAVED";

                StatusText.Foreground =
                    LimeBrush;
            }
            else
            {
                UpdatePaymentUI(
                    Drinks.PaymentEnabled);

                StatusText.Text =
                    "SAVE FAILED";

                StatusText.Foreground =
                    ErrorBrush;
            }
        }


        // =========================================================
        // SAVE
        // =========================================================

        private async void SaveButton_Click(
            object? sender,
            RoutedEventArgs e)
        {
            if (_isSaving)
                return;

            _isSaving = true;

            string mid =
                MidTextBox.Text?.Trim() ?? "";

            bool saved =
                Drinks.SetMerchantId(mid);

            if (saved)
            {
                MidTextBox.Text =
                    Drinks.MerchantId;

                StatusText.Text =
                    "SAVED";

                StatusText.Foreground =
                    LimeBrush;

                // -------------------------------------------------
                // WAIT 2 SECONDS
                // -------------------------------------------------

                await Task.Delay(1000);

                // -------------------------------------------------
                // RETURN TO ADMIN
                // -------------------------------------------------

                BackRequested?.Invoke();
            }
            else
            {
                StatusText.Text =
                    "SAVE FAILED";

                StatusText.Foreground =
                    ErrorBrush;
            }

            _isSaving = false;
        }


        // =========================================================
        // CANCEL
        // =========================================================

        private void CancelButton_Click(
            object? sender,
            RoutedEventArgs e)
        {
            if (_isSaving)
                return;

            BackRequested?.Invoke();
        }

        // =========================================================
        // ON-SCREEN KEYPAD METHODS
        // =========================================================

        private void CollectLetterButtons(StackPanel? panel)
        {
            if (panel == null) return;
            foreach (var child in panel.Children)
            {
                if (child is Button btn && btn.Classes.Contains("keypadLetter"))
                {
                    _letterButtons.Add(btn);
                }
            }
        }

        private void KeypadChar_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string val || string.IsNullOrEmpty(val)) return;
            string charToInsert = _isCaps ? val.ToUpperInvariant() : val.ToLowerInvariant();
            InsertText(charToInsert);
        }

        private void KeypadSpace_Click(object? sender, RoutedEventArgs e)
        {
            InsertText(" ");
        }

        private void InsertText(string textToInsert)
        {
            string current = MidTextBox.Text ?? "";
            int caret = MidTextBox.CaretIndex;

            if (caret < 0 || caret > current.Length)
            {
                caret = current.Length;
            }

            string updated = current.Insert(caret, textToInsert);
            MidTextBox.Text = updated;
            MidTextBox.CaretIndex = caret + textToInsert.Length;
        }

        private void KeypadBackspace_Click(object? sender, RoutedEventArgs e)
        {
            string current = MidTextBox.Text ?? "";
            if (current.Length == 0) return;

            int caret = MidTextBox.CaretIndex;
            if (caret <= 0 || caret > current.Length)
            {
                caret = current.Length;
            }

            if (caret > 0)
            {
                string updated = current.Remove(caret - 1, 1);
                MidTextBox.Text = updated;
                MidTextBox.CaretIndex = caret - 1;
            }
        }

        private void KeypadClear_Click(object? sender, RoutedEventArgs e)
        {
            MidTextBox.Text = "";
            MidTextBox.CaretIndex = 0;
        }

        private void KeypadCaps_Click(object? sender, RoutedEventArgs e)
        {
            _isCaps = !_isCaps;
            UpdateCapsVisual();
        }

        private void UpdateCapsVisual()
        {
            if (_capsBtn != null)
            {
                _capsBtn.Background = _isCaps ? LimeBrush : new SolidColorBrush(Color.Parse("#181818"));
                _capsBtn.Foreground = _isCaps ? BlackBrush : WhiteBrush;
                _capsBtn.BorderBrush = _isCaps ? LimeBrush : DarkLimeBrush;
            }

            foreach (var btn in _letterButtons)
            {
                if (btn.Tag is string tag)
                {
                    btn.Content = _isCaps ? tag.ToUpperInvariant() : tag.ToLowerInvariant();
                }
            }
        }
    }
}
