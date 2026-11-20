
using Avalonia.Controls;
using AvaloniaApplication2.Assets.Pages;
using AvaloniaApplication2.Services;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AvaloniaApplication2;

public partial class MainWindow : Window
{
    private static readonly HttpClient _hardwareHttpClient =
        new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

    private static readonly HttpClient _vendiBiteHttpClient =
        new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    public MainWindow()
    {
        InitializeComponent();
        KeyDown += MainWindow_KeyDown;
        ShowHomePage();
    }

    private void MainWindow_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.F11)
        {
            WindowState = WindowState == WindowState.FullScreen
                ? WindowState.Normal
                : WindowState.FullScreen;
            e.Handled = true;
        }
    }

    // =========================================================
    // HOME PAGE
    // =========================================================

    private void ShowHomePage()
    {
        HomePage homePage = new HomePage();

        homePage.OrderRequested += HomePage_OrderRequested;
        homePage.AdminRequested += HomePage_AdminRequested;

        PageContent.Content = homePage;
    }

    private void HomePage_AdminRequested()
    {
        ShowAdminPage();
    }

    // =========================================================
    // ADMIN PAGE
    // =========================================================

    private void ShowAdminPage()
    {
        AdminPage adminPage = new AdminPage();

        adminPage.PaymentSettingsRequested +=
            AdminPage_PaymentSettingsRequested;

        adminPage.MilkSettingsRequested +=
            AdminPage_MilkSettingsRequested;

        adminPage.DrinkSettingsRequested +=
            AdminPage_DrinkSettingsRequested;

        adminPage.CalibrationRequested +=
            AdminPage_CalibrationRequested;

        adminPage.ExitRequested +=
            AdminPage_ExitRequested;

        adminPage.VMMSettingsRequested +=
            AdminPage_VMMSettingsRequested;

        adminPage.AvailableStockRequested +=
            AdminPage_AvailableStockRequested;

        adminPage.ChangePasswordRequested +=
            AdminPage_ChangePasswordRequested;

        PageContent.Content = adminPage;
    }

    // =========================================================
    // AVAILABLE STOCK
    // =========================================================

    private void AdminPage_AvailableStockRequested()
    {
        UpdateStockPage updateStockPage = new UpdateStockPage();

        updateStockPage.BackRequested +=
            UpdateStockPage_BackRequested;

        PageContent.Content = updateStockPage;
    }

    private void UpdateStockPage_BackRequested()
    {
        ShowAdminPage();
    }

    // =========================================================
    // CHANGE PASSWORD
    // =========================================================

    private void AdminPage_ChangePasswordRequested()
    {
        ChangePasswordPage changePasswordPage =
            new ChangePasswordPage();

        // SAVE PASSWORD
        changePasswordPage.SaveRequested += async () =>
        {
            // CHECK CURRENT PASSWORD
            if (changePasswordPage.CurrentPassword !=
                Drinks.AdminPassword)
            {
                changePasswordPage.ShowStatus(
                    "Current password is incorrect.");

                return;
            }

            // GET NEW PASSWORD
            string newPassword =
                changePasswordPage.NewPassword;

            // CHECK LENGTH
            if (newPassword.Length != 4)
            {
                changePasswordPage.ShowStatus(
                    "Password must contain exactly 4 digits.");

                return;
            }

            // CHECK DIGITS ONLY
            foreach (char c in newPassword)
            {
                if (c < '0' || c > '9')
                {
                    changePasswordPage.ShowStatus(
                        "Password must contain digits only.");

                    return;
                }
            }

            // CHECK CONFIRMATION
            if (newPassword !=
                changePasswordPage.ConfirmPassword)
            {
                changePasswordPage.ShowStatus(
                    "New passwords do not match.");

                return;
            }

            // SAVE TO DRINKS.JSON
            bool saved = Drinks.SetAdminPassword(newPassword);

            if (!saved)
            {
                changePasswordPage.ShowStatus(
                    "Failed to save password. Please try again.");

                return;
            }

            // SHOW SUCCESS MESSAGE
            changePasswordPage.ClearFields();

            changePasswordPage.ShowStatus(
                "Password changed successfully.",
                true);

            // WAIT 1 SECOND
            await Task.Delay(1000);

            // RETURN TO ADMIN SETTINGS
            ShowAdminPage();
        };

        // CANCEL -> ADMIN PAGE IMMEDIATELY
        changePasswordPage.CancelRequested +=
            () => ShowAdminPage();

        PageContent.Content = changePasswordPage;
    }

    // =========================================================
    // VMM SETTINGS
    // =========================================================

    private void AdminPage_VMMSettingsRequested()
    {
        VMMPage vmmPage = new VMMPage();

        vmmPage.SetValues(
            Drinks.MachineId ?? "",
            Drinks.MachineName ?? "");

        vmmPage.SaveRequested += VMMPage_SaveRequested;
        vmmPage.CancelRequested += VMMPage_CancelRequested;

        PageContent.Content = vmmPage;
    }

    private void VMMPage_SaveRequested(
        string machineId,
        string machineName)
    {
        bool saved = Drinks.SetVMMSettings(
            machineId ?? "",
            machineName ?? "");

        if (saved)
            ShowAdminPage();
    }

    private void VMMPage_CancelRequested()
    {
        ShowAdminPage();
    }

    // =========================================================
    // PAYMENT SETTINGS
    // =========================================================

    private void AdminPage_PaymentSettingsRequested()
    {
        PaymentSettingsPage paymentSettingsPage =
            new PaymentSettingsPage();

        paymentSettingsPage.BackRequested +=
            PaymentSettingsPage_BackRequested;

        PageContent.Content = paymentSettingsPage;
    }

    private void PaymentSettingsPage_BackRequested()
    {
        ShowAdminPage();
    }

    // =========================================================
    // MILK SETTINGS
    // =========================================================

    private void AdminPage_MilkSettingsRequested()
    {
        MilkSettingsPage milkSettingsPage =
            new MilkSettingsPage();

        milkSettingsPage.BackRequested +=
            MilkSettingsPage_BackRequested;

        PageContent.Content = milkSettingsPage;
    }

    private void MilkSettingsPage_BackRequested()
    {
        ShowAdminPage();
    }

    // =========================================================
    // DRINK SETTINGS
    // =========================================================

    private void AdminPage_DrinkSettingsRequested()
    {
        DrinkSettingsPage drinkSettingsPage =
            new DrinkSettingsPage();

        drinkSettingsPage.BackRequested +=
            DrinkSettingsPage_BackRequested;

        drinkSettingsPage.DrinkSelectedRequested +=
            DrinkSettingsPage_DrinkSelectedRequested;

        PageContent.Content = drinkSettingsPage;
    }

    private void DrinkSettingsPage_DrinkSelectedRequested(
        int drinkId)
    {
        DrinkEditPage drinkEditPage = new DrinkEditPage();

        drinkEditPage.LoadDrink(drinkId);

        // EDIT IMAGE
        drinkEditPage.EditImageRequested +=
            async selectedDrinkId =>
            {
                EditImageWindow imageWindow =
                    new EditImageWindow();

                imageWindow.LoadDrink(selectedDrinkId);

                await imageWindow.ShowDialog(this);
            };

        // EDIT NUTRITION
        drinkEditPage.EditNutritionRequested +=
            async selectedDrinkId =>
            {
                NutritionEditWindow nutritionWindow =
                    new NutritionEditWindow();

                nutritionWindow.LoadDrink(selectedDrinkId);

                await nutritionWindow.ShowDialog(this);
            };

        // SAVE -> DRINK SETTINGS
        drinkEditPage.SaveRequested +=
            AdminPage_DrinkSettingsRequested;

        // CANCEL -> DRINK SETTINGS
        drinkEditPage.CancelRequested +=
            AdminPage_DrinkSettingsRequested;

        PageContent.Content = drinkEditPage;
    }

    private void DrinkSettingsPage_BackRequested()
    {
        ShowAdminPage();
    }

    // =========================================================
    // CALIBRATION
    // =========================================================

    private void AdminPage_CalibrationRequested()
    {
        CalibrationPage calibrationPage =
            new CalibrationPage();

        calibrationPage.BackRequested +=
            CalibrationPage_BackRequested;

        PageContent.Content = calibrationPage;
    }

    private void CalibrationPage_BackRequested()
    {
        ShowAdminPage();
    }

    // =========================================================
    // EXIT ADMIN
    // =========================================================

    private void AdminPage_ExitRequested()
    {
        ShowHomePage();
    }

    // =========================================================
    // MENU PAGE
    // =========================================================

    private void HomePage_OrderRequested()
    {
        MenuPage menuPage = new MenuPage();

        menuPage.BackRequested += MenuPage_BackRequested;
        menuPage.ContinueRequested += MenuPage_ContinueRequested;

        PageContent.Content = menuPage;
    }

    private void MenuPage_BackRequested()
    {
        ShowHomePage();
    }

    // =========================================================
    // ADD-ONS PAGE
    // =========================================================

    private void MenuPage_ContinueRequested()
    {
        AddOnsPage addOnsPage = new AddOnsPage();

        addOnsPage.BackRequested += AddOnsPage_BackRequested;
        addOnsPage.HomeRequested += AddOnsPage_HomeRequested;
        addOnsPage.ContinueRequested +=
            AddOnsPage_ContinueRequested;

        PageContent.Content = addOnsPage;
    }

    private void AddOnsPage_BackRequested()
    {
        ShowMenuPage();
    }

    private void AddOnsPage_HomeRequested()
    {
        ShowHomePage();
    }

    private void ShowMenuPage()
    {
        MenuPage menuPage = new MenuPage();

        menuPage.BackRequested += MenuPage_BackRequested;
        menuPage.ContinueRequested += MenuPage_ContinueRequested;

        PageContent.Content = menuPage;
    }

    // =========================================================
    // PAYMENT PAGE
    // =========================================================

    private void AddOnsPage_ContinueRequested()
    {
        PaymentPage paymentPage = new PaymentPage();

        paymentPage.DrinkRequested += PaymentPage_DrinkRequested;
        paymentPage.HomeRequested += PaymentPage_HomeRequested;

        PageContent.Content = paymentPage;
    }

    private void PaymentPage_DrinkRequested()
    {
        DrinkData? selectedDrink =
            CurrentOrder.Drink ?? (CurrentOrder.DrinkId == 9 ? Bar.First : CurrentOrder.DrinkId == 10 ? Bar.Second : null);

        bool isBar = (selectedDrink != null && selectedDrink.IsBar) ||
                     CurrentOrder.DrinkId == 9 ||
                     CurrentOrder.DrinkId == 10;

        if (isBar)
        {
            DrinkData barItem = selectedDrink ?? (CurrentOrder.DrinkId == 10 ? Bar.Second : Bar.First);
            HandleBarOrderDirectPickup(barItem);
            return;
        }

        DrinkPage drinkPage = new DrinkPage();

        drinkPage.DrinkCompleted += DrinkPage_DrinkCompleted;
        drinkPage.DrinkFailed += DrinkPage_DrinkFailed;

        PageContent.Content = drinkPage;
    }

    private void HandleBarOrderDirectPickup(DrinkData selectedBar)
    {
        // 1. Verify and reduce stock
        if (!Drinks.ReduceStock(selectedBar.Id))
        {
            DrinkPage_DrinkFailed("The selected protein bar is currently out of stock.");
            return;
        }

        // 2. Send 15-bit packet to controller (Bit 9 for Bar 1, Bit 10 for Bar 2)
        _ = MachineBits.SendDrinkBitsAsync(_hardwareHttpClient, selectedBar.Id, "None");

        // 3. Send VendiBite order and stock updates asynchronously
        _ = SendVendiBiteBarUpdatesAsync(selectedBar);

        // 4. Jump directly to PickUpDrinkPage (Bar orders always return to HomePage)
        PickUpDrinkPage pickUpDrinkPage =
            new PickUpDrinkPage();

        pickUpDrinkPage.CompletedRequested +=
            ShowHomePage;

        pickUpDrinkPage.HomeRequested +=
            ShowHomePage;

        PageContent.Content = pickUpDrinkPage;
    }

    private static async Task SendVendiBiteBarUpdatesAsync(DrinkData selectedBar)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(CurrentOrder.OrderId))
            {
                await VendiBiteService.PlaceOrderAsync();
            }

            await VendiBiteService.UpdateStockAsync();
        }
        catch
        {
            // API failures must not interrupt machine operations
        }
    }

    private void DrinkPage_DrinkFailed(string errorMessage)
    {
        ErrorPage errorPage = new ErrorPage();
        errorPage.SetErrorInfo(errorMessage, Drinks.ServiceContactNumber);
        errorPage.HomeRequested += ErrorPage_HomeRequested;

        PageContent.Content = errorPage;
    }

    private void ErrorPage_HomeRequested()
    {
        ShowHomePage();
    }

    private void PaymentPage_HomeRequested()
    {
        ShowHomePage();
    }

    // =========================================================
    // DRINK PAGE
    // =========================================================

    private void DrinkPage_DrinkCompleted()
    {
        PickUpDrinkPage pickUpDrinkPage =
            new PickUpDrinkPage();

        pickUpDrinkPage.CompletedRequested +=
            PickUpDrinkPage_CompletedRequested;

        pickUpDrinkPage.HomeRequested +=
            ShowHomePage;

        PageContent.Content = pickUpDrinkPage;
    }

    // =========================================================
    // PICK UP DRINK PAGE
    // =========================================================

    private void PickUpDrinkPage_CompletedRequested()
    {
        CompletedPage completedPage = new CompletedPage();

        completedPage.HomeRequested +=
            CompletedPage_HomeRequested;

        PageContent.Content = completedPage;
    }

    private void CompletedPage_HomeRequested()
    {
        ShowHomePage();
    }
}
