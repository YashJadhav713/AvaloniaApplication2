using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System;

namespace AvaloniaApplication2.Assets.Pages
{
    public partial class AdminPage : UserControl
    {
        public event Action? PaymentSettingsRequested;
        public event Action? MilkSettingsRequested;
        public event Action? AvailableStockRequested;
        public event Action? DrinkSettingsRequested;
        public event Action? CalibrationRequested;
        public event Action? VMMSettingsRequested;
        public event Action? ChangePasswordRequested;
        public event Action? ExitRequested;

        public Button ExitButton => HomeButton;

        public AdminPage()
        {
            InitializeComponent();

            PaymentSettingsButton.Click += (_, _) =>
                PaymentSettingsRequested?.Invoke();

            MilkSettingsButton.Click += (_, _) =>
                MilkSettingsRequested?.Invoke();

            AvailableStockButton.Click += (_, _) =>
                AvailableStockRequested?.Invoke();

            DrinkSettingsButton.Click += (_, _) =>
                DrinkSettingsRequested?.Invoke();

            CalibrationButton.Click += (_, _) =>
                CalibrationRequested?.Invoke();

            VMMSettingsButton.Click += (_, _) =>
                VMMSettingsRequested?.Invoke();

            ChangePasswordButton.Click += (_, _) =>
                ChangePasswordRequested?.Invoke();

            HomeButton.Click += (_, _) =>
                ExitRequested?.Invoke();

            ExitAppButton.Click += (_, _) =>
            {
                ExitConfirmOverlay.IsVisible = true;
            };

            ConfirmExitNoButton.Click += (_, _) =>
            {
                ExitConfirmOverlay.IsVisible = false;
            };

            ConfirmExitYesButton.Click += (_, _) =>
            {
                try
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        desktop.Shutdown();
                    }
                }
                catch
                {
                }
                finally
                {
                    Environment.Exit(0);
                }
            };
        }
    }
}
