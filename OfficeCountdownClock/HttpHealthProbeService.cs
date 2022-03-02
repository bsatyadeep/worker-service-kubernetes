using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace OfficeCountdownClock
{
    public sealed class HttpHealthProbeService : BackgroundService
    {
        private readonly ILogger<HttpHealthProbeService> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpListener _httpListener;
        private readonly HealthCheckService _healthCheckService;
        private readonly string _apiName, _apiVersion, _healthProbePrefix;
        public HttpHealthProbeService(ILogger<HttpHealthProbeService> logger,
            IConfiguration configuration, HealthCheckService healthCheckService)
        {
            _logger = logger;
            _configuration = configuration;
            _httpListener = new HttpListener();
            _healthCheckService = healthCheckService;

            _healthProbePrefix = "http://localhost:5000/health/";
            _apiName = "Sample API";
            _apiVersion = "1.0.0";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _httpListener.Prefixes.Add(_healthProbePrefix);
            _httpListener.Start();
            _logger.LogDebug($"Healthcheck listening...");

            while (!stoppingToken.IsCancellationRequested)
            {
                HttpListenerContext httpListenerContext = null;
                try
                {
                    httpListenerContext = await _httpListener.GetContextAsync();
                }
                catch (HttpListenerException ex)
                {
                    if (ex.ErrorCode == 995) return;
                }

                if (httpListenerContext == null) continue;

                var result = await _healthCheckService.CheckHealthAsync(stoppingToken);
                var healthInfo = new
                {
                    Status = result.Status.ToString(),
                    HealthChecks = result.Entries.Select(x => new
                    {
                        Components = x.Key,
                        Status = x.Value.Status.ToString(),
                        Description = x.Value.Description,
                        ApiName = _apiName,
                        ApiVersion = _apiVersion,
                    }),
                    HealthCheckDuration = result.TotalDuration
                };
                var response = httpListenerContext.Response;
                response.ContentType = "application/json";
                response.Headers.Add(HttpResponseHeader.CacheControl, "no-store, no-cache");
                response.StatusCode = (int)HttpStatusCode.OK;

                var messageBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(healthInfo));
                response.ContentLength64 = messageBytes.Length;
                await response.OutputStream.WriteAsync(messageBytes, 0, messageBytes.Length);
                response.OutputStream.Close();
                response.Close();
            }
            _httpListener.Stop();
        }
    }
}
