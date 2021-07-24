using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using SerilogWebApi.DotNetCore31.Setup;

namespace SerilogWebApi.DotNetCore31
{
    public class Program
    {
        // Used to discriminate service/app logs in multi-service applications
        public static readonly string AppName =
            Path.GetFileNameWithoutExtension(typeof(Program).Assembly.Location);

        public static int Main(string[] args)
        {
            Console.WriteLine($"starting {AssemblyInfo.AssemblyName} - {AssemblyInfo.Version}...");

            /* Create a "fail-safe" logger available during startup
             * ---------------------------------------------------- */

            var seqUrl = Environment.GetEnvironmentVariable("SEQ_URL");
            var seqApiKey = Environment.GetEnvironmentVariable("SEQ_APIKEY");
            var instrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");

            var loggerConfiguration = new LoggerConfiguration()
                .ConfigureSerilogDefaults(AppName, seqUrl, seqApiKey)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                // Write to Application Insights with Serilog-created sink on startup
                .WriteTo.ApplicationInsights(instrumentationKey, TelemetryConverter.Traces);

            Log.Logger = loggerConfiguration.CreateBootstrapLogger();

            try
            {
                Log.Information("----- Configuring host ({ApplicationContext} - {AppVersion})...", AppName, AssemblyInfo.Version);
                var host = CreateHostBuilder(args).Build();

                // Include database migrations here (or better yet, Consider using a separate migrations assembly)

                Log.Information("----- Starting host ({ApplicationContext} - {AppVersion})...", AppName, AssemblyInfo.Version);
                host.Run();

                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "----- Program terminated unexpectedly ({ApplicationContext}): {ErrorMessage}", AppName, ex.Message);
                return 1;
            }
            finally
            {
                Log.Information("----- Host stopped ({ApplicationContext} - {AppVersion}).", AppName, AssemblyInfo.Version);
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>()
                        .CaptureStartupErrors(false); // So we can log startup errors
                })
                // This must be the last configuration 
                .UseSerilog((context, services, loggerConfiguration) =>
                {
                    var seqUrl = Environment.GetEnvironmentVariable("SEQ_URL");
                    var seqApiKey = Environment.GetEnvironmentVariable("SEQ_APIKEY");
                    var telemetryConfiguration = services.GetService<TelemetryConfiguration>();

                    loggerConfiguration
                        .ConfigureSerilogDefaults(AppName, seqUrl, seqApiKey, context.HostingEnvironment)
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                        // At this point we can use the standard Application Insights client. This ensures metrics and traces correlation
                        .WriteTo.ApplicationInsights(telemetryConfiguration, TelemetryConverter.Traces)
                        // Use Serilog configuration from IConfiguration and DI services
                        .ReadFrom.Configuration(context.Configuration);
                });
    }
}
