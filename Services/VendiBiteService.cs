using AvaloniaApplication2;
using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaApplication2.Services
{
    public static class VendiBiteService
    {
        private const string ApiUrl = "https://vmm.vendibite.com/machinetoserver.php";

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private static readonly SemaphoreSlim _orderLock = new SemaphoreSlim(1, 1);
        private static readonly object _syncObj = new object();
        private static Task<string?>? _inFlightOrderTask;

        /// <summary>
        /// Sends machine id, machine name, customer selected product, milk/water base to the VendiBite API.
        /// Receives the generated Order ID and stores it in CurrentOrder.OrderId.
        /// Deduplicated: only sends ONE HTTP request per order even if called multiple times concurrently.
        /// </summary>
        public static async Task<string?> PlaceOrderAsync()
        {
            // 1. If an Order ID is already present for this order, return it immediately without calling the API again.
            string existingOrderId = CurrentOrder.OrderId;
            if (!string.IsNullOrWhiteSpace(existingOrderId))
            {
                return existingOrderId;
            }

            // 2. If a request is already in-flight for this order, await that existing task instead of sending another.
            Task<string?>? runningTask;
            lock (_syncObj)
            {
                runningTask = _inFlightOrderTask;
            }

            if (runningTask != null)
            {
                return await runningTask;
            }

            await _orderLock.WaitAsync();
            try
            {
                // Double check under semaphore
                existingOrderId = CurrentOrder.OrderId;
                if (!string.IsNullOrWhiteSpace(existingOrderId))
                {
                    return existingOrderId;
                }

                lock (_syncObj)
                {
                    if (_inFlightOrderTask != null)
                    {
                        runningTask = _inFlightOrderTask;
                    }
                    else
                    {
                        _inFlightOrderTask = ExecutePlaceOrderInternalAsync();
                        runningTask = _inFlightOrderTask;
                    }
                }

                return await runningTask;
            }
            finally
            {
                lock (_syncObj)
                {
                    _inFlightOrderTask = null;
                }
                _orderLock.Release();
            }
        }

        private static async Task<string?> ExecutePlaceOrderInternalAsync()
        {
            try
            {
                string machineId = Drinks.MachineId?.Trim() ?? "";
                string machineName = Drinks.MachineName?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(machineId))
                {
                    return null;
                }

                // If machine name is empty, default to "NutriPod"
                if (string.IsNullOrWhiteSpace(machineName))
                {
                    machineName = "NutriPod";
                }

                string drinkName = CurrentOrder.DrinkName;
                if (string.IsNullOrWhiteSpace(drinkName))
                {
                    drinkName = CurrentOrder.Drink?.Name ?? "Drink";
                }

                string baseType = CurrentOrder.Base; // "Water", "Milk", or "None"

                string amount = CurrentOrder.Price?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(amount) ||
                    (double.TryParse(amount, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedAmt) && parsedAmt <= 0))
                {
                    amount = "1";
                }

                // Order details with dynamic machine name, drink name, and base (no date/time)
                string orderDetails = $"{machineName} - {drinkName} - Base: {baseType}";

                string requestUrl =
                    $"{ApiUrl}?action=place_order" +
                    $"&machine_id={Uri.EscapeDataString(machineId)}" +
                    $"&machine_name={Uri.EscapeDataString(machineName)}" +
                    $"&order_details={Uri.EscapeDataString(orderDetails)}" +
                    $"&amount={Uri.EscapeDataString(amount)}";

                using HttpResponseMessage response = await _httpClient.GetAsync(requestUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                string responseBody = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    return null;
                }

                using JsonDocument doc = JsonDocument.Parse(responseBody);
                JsonElement root = doc.RootElement;

                bool success = false;
                if (root.TryGetProperty("success", out JsonElement successEl))
                {
                    if (successEl.ValueKind == JsonValueKind.True) success = true;
                    else if (successEl.ValueKind == JsonValueKind.False) success = false;
                    else if (successEl.ValueKind == JsonValueKind.String) bool.TryParse(successEl.GetString(), out success);
                }

                if (success && root.TryGetProperty("order_id", out JsonElement orderIdEl))
                {
                    string orderId = orderIdEl.ToString();
                    if (!string.IsNullOrWhiteSpace(orderId))
                    {
                        CurrentOrder.SetOrderId(orderId);
                        return orderId;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VendiBiteService] PlaceOrder error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Sends updated local inventory to VendiBite server to store updated stock (no date/time).
        /// </summary>
        public static async Task<bool> UpdateStockAsync()
        {
            try
            {
                string machineId = Drinks.MachineId?.Trim() ?? "";
                string machineName = Drinks.MachineName?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(machineId))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(machineName))
                {
                    machineName = "NutriPod";
                }

                DrinkData[] drinks =
                {
                    Drink.First,
                    Drink.Second,
                    Drink.Third,
                    Drink.Fourth,
                    Drink.Fifth,
                    Drink.Sixth,
                    Drink.Seventh,
                    Drink.Eighth
                };

                DrinkData bar1 = Bar.First;
                DrinkData bar2 = Bar.Second;
                int bar1Stock = Drinks.GetStock(9);
                int bar2Stock = Drinks.GetStock(10);
                int milkStock = Drinks.GetMilkStock();

                var stock = new
                {
                    name1 = drinks[0].Name,
                    quantity1 = Drinks.GetStock(1),
                    status1 = drinks[0].Enabled ? 1 : 0,

                    name2 = drinks[1].Name,
                    quantity2 = Drinks.GetStock(2),
                    status2 = drinks[1].Enabled ? 1 : 0,

                    name3 = drinks[2].Name,
                    quantity3 = Drinks.GetStock(3),
                    status3 = drinks[2].Enabled ? 1 : 0,

                    name4 = drinks[3].Name,
                    quantity4 = Drinks.GetStock(4),
                    status4 = drinks[3].Enabled ? 1 : 0,

                    name5 = drinks[4].Name,
                    quantity5 = Drinks.GetStock(5),
                    status5 = drinks[4].Enabled ? 1 : 0,

                    name6 = drinks[5].Name,
                    quantity6 = Drinks.GetStock(6),
                    status6 = drinks[5].Enabled ? 1 : 0,

                    name7 = drinks[6].Name,
                    quantity7 = Drinks.GetStock(7),
                    status7 = drinks[6].Enabled ? 1 : 0,

                    name8 = drinks[7].Name,
                    quantity8 = Drinks.GetStock(8),
                    status8 = drinks[7].Enabled ? 1 : 0,

                    name9 = "Milk",
                    quantity9 = milkStock,
                    status9 = Drinks.MilkEnabled ? 1 : 0,

                    // Bar 1 (ID 9)
                    name10 = bar1.Name,
                    quantity10 = bar1Stock,
                    status10 = bar1.Enabled ? 1 : 0,

                    // Bar 2 (ID 10)
                    name11 = bar2.Name,
                    quantity11 = bar2Stock,
                    status11 = bar2.Enabled ? 1 : 0,

                    // Bar explicit fields for backends checking bar1 / bar2 keys
                    bar1_name = bar1.Name,
                    bar1_quantity = bar1Stock,
                    bar1_stock = bar1Stock,
                    bar1_status = bar1.Enabled ? 1 : 0,

                    bar2_name = bar2.Name,
                    bar2_quantity = bar2Stock,
                    bar2_stock = bar2Stock,
                    bar2_status = bar2.Enabled ? 1 : 0,

                    bar1 = bar1Stock,
                    bar2 = bar2Stock
                };

                string stockJson = JsonSerializer.Serialize(new[] { stock });
                string requestUrl =
                    $"{ApiUrl}?action=update_stock" +
                    $"&machine_unique_id={Uri.EscapeDataString(machineId)}" +
                    $"&machine_id={Uri.EscapeDataString(machineId)}" +
                    $"&machine_name={Uri.EscapeDataString(machineName)}" +
                    $"&stock_data={Uri.EscapeDataString(stockJson)}" +
                    $"&bar1_stock={bar1Stock}" +
                    $"&bar2_stock={bar2Stock}" +
                    $"&bar1_quantity={bar1Stock}" +
                    $"&bar2_quantity={bar2Stock}" +
                    $"&bar1_status={(bar1.Enabled ? 1 : 0)}" +
                    $"&bar2_status={(bar2.Enabled ? 1 : 0)}" +
                    $"&quantity10={bar1Stock}" +
                    $"&quantity11={bar2Stock}" +
                    $"&status10={(bar1.Enabled ? 1 : 0)}" +
                    $"&status11={(bar2.Enabled ? 1 : 0)}";

                using HttpResponseMessage response = await _httpClient.GetAsync(requestUrl);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VendiBiteService] UpdateStock error: {ex.Message}");
                return false;
            }
        }
    }
}
