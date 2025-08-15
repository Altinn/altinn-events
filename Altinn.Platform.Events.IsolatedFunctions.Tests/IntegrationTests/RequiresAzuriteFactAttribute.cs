using System.Net.Sockets;

namespace Altinn.Platform.Events.IsolatedFunctions.Tests.IntegrationTests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresAzuriteFactAttribute : FactAttribute
{
    private static readonly object _sync = new();
    private static bool? _available;

    public RequiresAzuriteFactAttribute()
    {
        if (!IsAzuriteAvailable())
        {
            Skip = "Skipped: Azurite not detected (set AZURITE=1 and ensure Azurite running on 127.0.0.1:10001).";
        }
    }

    private static bool IsAzuriteAvailable()
    {
        lock (_sync)
        {
            if (_available.HasValue) return _available.Value;

            // Allow explicit override
            var flag = Environment.GetEnvironmentVariable("AZURITE");
            if (string.Equals(flag, "1", StringComparison.OrdinalIgnoreCase))
            {
                _available = true;
                return true;
            }

            try
            {
                using var client = new TcpClient();
                var task = client.ConnectAsync("127.0.0.1", 10001);
                if (!task.Wait(TimeSpan.FromMilliseconds(250)))
                {
                    _available = false;
                }
                else
                {
                    _available = client.Connected;
                }
            }
            catch
            {
                _available = false;
            }

            return _available.Value;
        }
    }
}
