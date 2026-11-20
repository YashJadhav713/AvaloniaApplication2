using Avalonia;
using System;
using System.Threading;

namespace AvaloniaApplication2;

class Program
{
    private static Mutex? _singleInstanceMutex;

    // Initialization code.
    [STAThread]
    public static void Main(string[] args)
    {
        const string mutexName =
            "SHAKE_LAB_SINGLE_INSTANCE";

        _singleInstanceMutex =
            new Mutex(
                true,
                mutexName,
                out bool createdNew);

        if (!createdNew)
        {
            return;
        }

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            try
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            catch
            {
                // Ignore mutex release errors on teardown
            }
            _singleInstanceMutex.Dispose();
        }
    }

    // Avalonia configuration.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}