using Destructurama;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;

namespace SerilogWebApi.DotNetCore31.Setup
{
    public static class LoggingConfigurationExtensions
    {
        public static LoggerConfiguration ConfigureSerilogDefaults(
            this LoggerConfiguration loggerConfiguration,
            string appName,
            string seqUrl = null,
            string seqApiKey = null,
            IHostEnvironment hostEnvironment = null)
        {
            loggerConfiguration
                .MinimumLevel.Verbose()
                .Enrich.WithProperty("ApplicationContext", appName)
                .Enrich.WithMachineName()
                .Enrich.FromLogContext()
                .Destructure.JsonNetTypes() // Enable JObject destruturing
                .WriteTo.Console()
                .WriteTo.File( // Write to standard Azure AppService-monitored log location
                    $@"D:\home\LogFiles\{appName}-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    rollOnFileSizeLimit: true,
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(1));

            var environment = hostEnvironment is null
                ? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                : hostEnvironment.EnvironmentName;

            // Add Seq for development by default or always if configured
            if (string.IsNullOrWhiteSpace(seqUrl))
            {
                if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
                {
                    loggerConfiguration.WriteTo.Seq("http://localhost:5341");
                }
            }
            else
            {
                loggerConfiguration.WriteTo.Seq(seqUrl, apiKey: seqApiKey);
            }

            return loggerConfiguration;
        }
    }
}
