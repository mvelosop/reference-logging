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
using SerilogWebApi.DotNetCore31.Configuration;

namespace SerilogWebApi.DotNetCore31
{
    public class Program
    {
        // Used to discriminate service/app logs in multi-service applications
        public static readonly string AppName =
            Path.GetFileNameWithoutExtension(typeof(Program).Assembly.Location);

        // Used to discriminate host logs in multi-host environments
        public static readonly string HostName =
            Environment.GetEnvironmentVariable("HOSTNAME") ?? // For Linux
            Environment.GetEnvironmentVariable("COMPUTERNAME"); // For Windows

        public static int Main(string[] args)
        {
            /* Create a "fail-safe" logger available during startup
             * ---------------------------------------------------- */

            var seqUrl = Environment.GetEnvironmentVariable("SEQ_URL");
            var instrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");

            var loggerConfiguration = new LoggerConfiguration()
                .ConfigureSerilogDefaults(seqUrl, AppName, HostName)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                // Write to Application Insights with Serilog-created sink on startup
                .WriteTo.ApplicationInsights(instrumentationKey, TelemetryConverter.Traces);

            Log.Logger = loggerConfiguration.CreateBootstrapLogger();

            Console.Write($"{AssemblyInfo.AssemblyName} - {AssemblyInfo.Version}");

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
                    var telemetryConfiguration = services.GetService<TelemetryConfiguration>();

                    loggerConfiguration
                        .ConfigureSerilogDefaults(seqUrl, AppName, HostName, context.HostingEnvironment)
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                        // At this point we can use the standard Application Insights client. This ensures metrics and traces correlation
                        .WriteTo.ApplicationInsights(telemetryConfiguration, TelemetryConverter.Traces)
                        // Use Serilog configuration from IConfiguration and DI services
                        .ReadFrom.Configuration(context.Configuration);
                });
    }
}
