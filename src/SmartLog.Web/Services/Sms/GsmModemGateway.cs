using System.Diagnostics;
using System.IO.Ports;
using System.Text;

namespace SmartLog.Web.Services.Sms;

/// <summary>
/// GSM Modem SMS gateway using AT commands via serial port
/// Works offline - no internet required
/// </summary>
public class GsmModemGateway : ISmsGateway, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GsmModemGateway> _logger;
    private SerialPort? _serialPort;
    private readonly SemaphoreSlim _portLock = new(1, 1);
    private DateTime _lastSendTime = DateTime.MinValue;

    // Circuit breaker: skip modem attempts after consecutive failures
    private int _consecutiveFailures;
    private DateTime _circuitOpenUntil = DateTime.MinValue;
    private const int CircuitBreakerThreshold = 3;
    private static readonly TimeSpan CircuitBreakerCooldown = TimeSpan.FromMinutes(5);

    public string ProviderName => "GSM_MODEM";

    public GsmModemGateway(
        IConfiguration configuration,
        ILogger<GsmModemGateway> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> IsAvailableAsync()
    {
        // Circuit breaker: skip if recently failed too many times
        if (_consecutiveFailures >= CircuitBreakerThreshold && DateTime.UtcNow < _circuitOpenUntil)
        {
            _logger.LogDebug("GSM modem circuit breaker open until {Until}, skipping availability check",
                _circuitOpenUntil);
            return false;
        }

        await _portLock.WaitAsync();
        try
        {
            EnsurePortOpen();
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                RecordFailure();
                return false;
            }

            // Test with AT command
            var response = await SendAtCommandAsync("AT", 1000);
            var available = response.Contains("OK");
            if (available)
            {
                _consecutiveFailures = 0; // Reset on success
            }
            else
            {
                RecordFailure();
            }
            return available;
        }
        catch (Exception ex)
        {
            RecordFailure();
            _logger.LogWarning(ex, "GSM modem not available (failure {Count}/{Threshold})",
                _consecutiveFailures, CircuitBreakerThreshold);
            return false;
        }
        finally
        {
            _portLock.Release();
        }
    }

    private void RecordFailure()
    {
        _consecutiveFailures++;
        if (_consecutiveFailures >= CircuitBreakerThreshold)
        {
            _circuitOpenUntil = DateTime.UtcNow.Add(CircuitBreakerCooldown);
            _logger.LogWarning("GSM modem circuit breaker opened after {Count} consecutive failures. " +
                "Will retry after {Until}. All SMS routed to fallback gateway.",
                _consecutiveFailures, _circuitOpenUntil);
        }
    }

    public async Task<SmsSendResult> SendAsync(string phoneNumber, string message)
    {
        var stopwatch = Stopwatch.StartNew();
        await _portLock.WaitAsync();

        try
        {
            // Enforce rate limiting (3 second delay between messages)
            var sendDelayMs = _configuration.GetValue<int>("Sms:GsmModem:SendDelayMs", 3000);
            var timeSinceLastSend = DateTime.UtcNow - _lastSendTime;
            if (timeSinceLastSend.TotalMilliseconds < sendDelayMs)
            {
                var remainingDelay = sendDelayMs - (int)timeSinceLastSend.TotalMilliseconds;
                await Task.Delay(remainingDelay);
            }

            EnsurePortOpen();
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                throw new InvalidOperationException("Serial port is not open");
            }

            // Normalize phone number (09xx to +639xx)
            var normalizedPhone = NormalizePhoneNumber(phoneNumber);

            // Set SMS text mode
            var response = await SendAtCommandAsync("AT+CMGF=1", 1000);
            if (!response.Contains("OK"))
            {
                throw new Exception("Failed to set SMS text mode");
            }

            // Set GSM character set
            response = await SendAtCommandAsync("AT+CSCS=\"GSM\"", 1000);
            if (!response.Contains("OK"))
            {
                _logger.LogWarning("Failed to set GSM character set, continuing anyway");
            }

            // Send message
            response = await SendAtCommandAsync($"AT+CMGS=\"{normalizedPhone}\"", 1000);
            if (!response.Contains(">"))
            {
                throw new Exception("Modem did not return prompt for message");
            }

            // Send message content followed by Ctrl+Z (ASCII 26)
            _serialPort.Write(message);
            _serialPort.Write(new byte[] { 26 }, 0, 1);

            // Wait for response (can take up to 30 seconds)
            response = await ReadResponseAsync(30000);

            _lastSendTime = DateTime.UtcNow;
            stopwatch.Stop();

            if (response.Contains("OK") || response.Contains("+CMGS"))
            {
                // Extract message ID if present
                var messageId = ExtractMessageId(response);
                var messageParts = CalculateMessageParts(message);

                _consecutiveFailures = 0; // Reset circuit breaker on successful send
                _logger.LogInformation("SMS sent via GSM modem to {Phone} in {Ms}ms",
                    normalizedPhone, stopwatch.ElapsedMilliseconds);

                return new SmsSendResult
                {
                    Success = true,
                    ProviderMessageId = messageId,
                    ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds,
                    MessageParts = messageParts
                };
            }
            else
            {
                throw new Exception($"Failed to send SMS: {response}");
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error sending SMS via GSM modem to {Phone}", phoneNumber);

            return new SmsSendResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        finally
        {
            _portLock.Release();
        }
    }

    public async Task<GatewayHealthStatus> GetHealthStatusAsync()
    {
        var status = new GatewayHealthStatus
        {
            IsHealthy = false,
            Status = "Offline",
            Details = new Dictionary<string, string>()
        };

        await _portLock.WaitAsync();
        try
        {
            EnsurePortOpen();
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                status.Details["Error"] = "Serial port not open";
                return status;
            }

            // Check basic connectivity
            var response = await SendAtCommandAsync("AT", 1000);
            if (!response.Contains("OK"))
            {
                status.Details["Error"] = "Modem not responding";
                return status;
            }

            // Check SIM card status
            response = await SendAtCommandAsync("AT+CPIN?", 1000);
            if (response.Contains("READY"))
            {
                status.Details["SIM"] = "Ready";
            }
            else if (response.Contains("SIM PIN"))
            {
                status.Details["SIM"] = "PIN required";
                status.Status = "SIM locked";
                return status;
            }
            else
            {
                status.Details["SIM"] = "Not ready";
                status.Status = "SIM error";
                return status;
            }

            // Check signal strength
            response = await SendAtCommandAsync("AT+CSQ", 1000);
            var signalStrength = ExtractSignalStrength(response);
            status.Details["Signal"] = signalStrength;

            // Check network registration
            response = await SendAtCommandAsync("AT+CREG?", 1000);
            if (response.Contains(",1") || response.Contains(",5"))
            {
                status.Details["Network"] = "Registered";
                status.IsHealthy = true;
                status.Status = "Online";
            }
            else
            {
                status.Details["Network"] = "Not registered";
                status.Status = "No network";
            }

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking GSM modem health");
            status.Details["Error"] = ex.Message;
            return status;
        }
        finally
        {
            _portLock.Release();
        }
    }

    private void EnsurePortOpen()
    {
        if (_serialPort != null && _serialPort.IsOpen)
        {
            return;
        }

        var portName = _configuration.GetValue<string>("Sms:GsmModem:PortName", "COM3");
        var baudRate = _configuration.GetValue<int>("Sms:GsmModem:BaudRate", 9600);

        _serialPort = new SerialPort(portName, baudRate)
        {
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            ReadTimeout = 5000,
            WriteTimeout = 5000,
            NewLine = "\r\n",
            Encoding = Encoding.ASCII
        };

        try
        {
            _serialPort.Open();
            _logger.LogInformation("Opened serial port {Port} at {BaudRate} baud", portName, baudRate);

            // Clear any pending data
            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open serial port {Port}", portName);
            throw;
        }
    }

    private async Task<string> SendAtCommandAsync(string command, int timeoutMs)
    {
        if (_serialPort == null || !_serialPort.IsOpen)
        {
            throw new InvalidOperationException("Serial port is not open");
        }

        _serialPort.DiscardInBuffer();
        _serialPort.WriteLine(command);
        _logger.LogDebug("Sent AT command: {Command}", command);

        return await ReadResponseAsync(timeoutMs);
    }

    private async Task<string> ReadResponseAsync(int timeoutMs)
    {
        if (_serialPort == null)
        {
            throw new InvalidOperationException("Serial port is null");
        }

        var response = new StringBuilder();
        var startTime = DateTime.UtcNow;

        while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
        {
            try
            {
                if (_serialPort.BytesToRead > 0)
                {
                    var line = _serialPort.ReadLine();
                    response.AppendLine(line);
                    _logger.LogDebug("Received: {Line}", line);

                    if (line.Contains("OK") || line.Contains("ERROR") || line.Contains("+CMGS"))
                    {
                        break;
                    }
                }
                else
                {
                    await Task.Delay(100);
                }
            }
            catch (TimeoutException)
            {
                // Continue waiting
                await Task.Delay(100);
            }
        }

        return response.ToString();
    }

    private string NormalizePhoneNumber(string phoneNumber)
    {
        // Remove all non-digit characters
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());

        // Convert 09xxxxxxxxx to +639xxxxxxxxx
        if (digits.StartsWith("09") && digits.Length == 11)
        {
            return "+63" + digits.Substring(1);
        }

        // Already in international format
        if (digits.StartsWith("63") && digits.Length == 12)
        {
            return "+" + digits;
        }

        // Return as-is with + prefix
        return phoneNumber.StartsWith("+") ? phoneNumber : "+" + phoneNumber;
    }

    private string? ExtractMessageId(string response)
    {
        // Example response: +CMGS: 123
        var lines = response.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("+CMGS:"))
            {
                var parts = line.Split(':');
                if (parts.Length > 1)
                {
                    return parts[1].Trim();
                }
            }
        }
        return null;
    }

    private int CalculateMessageParts(string message)
    {
        // Standard SMS is 160 chars, multipart is 153 chars per part
        var length = message.Length;
        if (length <= 160) return 1;
        return (int)Math.Ceiling((double)length / 153);
    }

    private string ExtractSignalStrength(string response)
    {
        // Example response: +CSQ: 20,0
        var lines = response.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("+CSQ:"))
            {
                var parts = line.Split(':');
                if (parts.Length > 1)
                {
                    var values = parts[1].Split(',');
                    if (values.Length > 0 && int.TryParse(values[0].Trim(), out var rssi))
                    {
                        if (rssi == 99) return "Unknown";
                        if (rssi >= 20) return "Excellent";
                        if (rssi >= 15) return "Good";
                        if (rssi >= 10) return "Fair";
                        if (rssi >= 5) return "Poor";
                        return "Very poor";
                    }
                }
            }
        }
        return "Unknown";
    }

    public void Dispose()
    {
        _serialPort?.Dispose();
        _portLock?.Dispose();
    }
}
