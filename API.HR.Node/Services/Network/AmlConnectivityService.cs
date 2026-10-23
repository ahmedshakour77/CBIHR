using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace API.HR.Node.Services.Network;

public sealed class AmlConnectivityService : BackgroundService, IAmlConnectivityService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AmlConnectivityService> _logger;

    private readonly string _targetDomain;
    private readonly TimeSpan _checkInterval;
    private readonly bool? _forceEnabled;

    private volatile bool _isConnected;

    public event Action<bool>? OnConnectivityChanged;

    public AmlConnectivityService(
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AmlConnectivityService> logger)
    {
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;

        _targetDomain = _configuration["AmlConnectivity:Domain"]
            ?? _configuration["ActiveDirectory:Domain"]
            ?? "aml.lan";

        var intervalSeconds = _configuration.GetValue<int?>("AmlConnectivity:CheckIntervalSeconds") ?? 30;
        _checkInterval = TimeSpan.FromSeconds(Math.Max(10, intervalSeconds));

        _forceEnabled = _configuration.GetValue<bool?>("AmlConnectivity:ForceEnabled");
    }

    public bool IsAmlConnected
    {
        get
        {
            if (_forceEnabled.HasValue)
            {
                return _forceEnabled.Value;
            }

            // إذا كان الطلب الحالي قادماً عبر نطاق aml.lan (مثل portal.aml.lan)
            try
            {
                var host = _httpContextAccessor.HttpContext?.Request.Host.Host;
                if (!string.IsNullOrWhiteSpace(host) && host.EndsWith(_targetDomain, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch { }

            return _isConnected;
        }
    }

    public async Task<bool> CheckConnectivityAsync(CancellationToken ct = default)
    {
        if (_forceEnabled.HasValue)
        {
            SetConnected(_forceEnabled.Value);
            return _forceEnabled.Value;
        }

        bool connected = await ProbeDomainAsync(_targetDomain, ct);
        SetConnected(connected);
        return connected;
    }

    private void SetConnected(bool value)
    {
        bool changed = _isConnected != value;
        _isConnected = value;

        if (changed)
        {
            _logger.LogInformation("[AML Network] Connectivity to '{Domain}' changed to: {Status}", _targetDomain, value ? "CONNECTED" : "DISCONNECTED");
            try
            {
                OnConnectivityChanged?.Invoke(value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AML Network] Error invoking OnConnectivityChanged event handler");
            }
        }
    }

    private static async Task<bool> ProbeDomainAsync(string domain, CancellationToken ct)
    {
        // 1. فحص تحليل اسم النطاق عبر DNS
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(domain, ct);
            if (addresses != null && addresses.Length > 0)
            {
                // إذا تم تحليل النطاق بنجاح، نجرب فتح اتصال TCP خفيف (LDAP 389, DNS 53, HTTPS 443, HTTP 80)
                int[] probePorts = [389, 53, 88, 443, 80];
                foreach (var port in probePorts)
                {
                    try
                    {
                        using var tcp = new TcpClient();
                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(700));

                        await tcp.ConnectAsync(addresses[0], port, timeoutCts.Token);
                        if (tcp.Connected)
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // فحص المنفذ التالي
                    }
                }

                // وجود عنوان IP محلل للدومين يعتبر تأكيداً على اتصال النطاق بالـ DNS
                return true;
            }
        }
        catch
        {
            // فشل تحليل DNS
        }

        // 2. فحص البينغ (Ping) كإجراء بديل
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(domain, 600);
            if (reply.Status == IPStatus.Success)
            {
                return true;
            }
        }
        catch
        {
            // فشل البينغ
        }

        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // فحص أولي عند بدء التشغيل
        try
        {
            await CheckConnectivityAsync(stoppingToken);
        }
        catch { }

        using var timer = new PeriodicTimer(_checkInterval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CheckConnectivityAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[AML Network] Periodic connectivity check error");
            }
        }
    }
}
