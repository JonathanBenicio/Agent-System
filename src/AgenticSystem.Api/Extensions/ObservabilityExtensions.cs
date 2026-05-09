using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

namespace AgenticSystem.Api.Extensions;

public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddEnterpriseObservability(this WebApplicationBuilder builder, string serviceName = "AgenticSystem")
    {
        // 1. Serilog Setup
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithEnvironmentName()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.WithProperty("Application", serviceName)
                .WriteTo.Console();
        });

        // 2. OpenTelemetry Setup
        var otelEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var useOtel = !string.IsNullOrWhiteSpace(otelEndpoint);

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName));

        // Tracing
        otel.WithTracing(tracing =>
        {
            tracing.AddAspNetCoreInstrumentation(opts => 
            {
                opts.Filter = context => !context.Request.Path.Value?.Contains("/health") ?? true;
            });
            tracing.AddHttpClientInstrumentation();
            tracing.AddEntityFrameworkCoreInstrumentation(opts =>
            {
                opts.SetDbStatementForText = true;
            });

            if (useOtel)
            {
                tracing.AddOtlpExporter();
            }
        });

        // Metrics
        otel.WithMetrics(metrics =>
        {
            metrics.AddAspNetCoreInstrumentation();
            metrics.AddHttpClientInstrumentation();
            metrics.AddRuntimeInstrumentation();
            metrics.AddMeter(
                "Microsoft.AspNetCore.Hosting",
                "Microsoft.AspNetCore.Server.Kestrel",
                "AgenticSystem.Runtime" // Custom metrics for our coordinator
            );

            if (useOtel)
            {
                metrics.AddOtlpExporter();
            }
        });

        // Logging
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            if (useOtel)
            {
                logging.AddOtlpExporter();
            }
        });

        return builder;
    }
}
