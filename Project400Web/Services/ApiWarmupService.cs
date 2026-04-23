using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Project400Web.Services;

/// <summary>
/// Warms up the API container when the web app starts.
/// Prevents cold-start delays when both containers scale to zero on Azure Container Apps.
/// </summary>
public class ApiWarmupService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiWarmupService> _logger;

    public ApiWarmupService(IHttpClientFactory httpClientFactory, ILogger<ApiWarmupService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give the web app a moment to finish starting up
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        var client = _httpClientFactory.CreateClient("Project400API");

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                _logger.LogInformation("API warmup attempt {Attempt}/5", attempt);
                var response = await client.GetAsync("/health", stoppingToken);
                _logger.LogInformation("API warmup succeeded with status {StatusCode}", response.StatusCode);
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "API warmup attempt {Attempt}/5 failed, retrying...", attempt);
                await Task.Delay(TimeSpan.FromSeconds(3 * attempt), stoppingToken);
            }
        }

        _logger.LogWarning("API warmup failed after 5 attempts — API may still be cold");
    }
}
