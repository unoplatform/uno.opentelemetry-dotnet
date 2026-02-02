// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Internal.Tests;

public sealed class PeriodicExportingMetricReaderHelperTests : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodicExportingMetricReaderHelperTests"/> class.
    /// </summary>
    public PeriodicExportingMetricReaderHelperTests()
    {
        ClearEnvVars();
    }

    /// <summary>
    /// Disposes the test fixture by clearing environment variables.
    /// </summary>
    public void Dispose()
    {
        ClearEnvVars();
    }

    /// <summary>
    /// Validates that the helper uses built-in defaults when no overrides exist.
    /// </summary>
    [Fact]
    public void CreatePeriodicExportingMetricReader_Defaults()
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        var reader = CreatePeriodicExportingMetricReader();
#pragma warning restore CA2000 // Dispose objects before losing scope

        Assert.Equal(60000, reader.ExportIntervalMilliseconds);
        Assert.Equal(30000, reader.ExportTimeoutMilliseconds);
        Assert.Equal(MetricReaderTemporalityPreference.Cumulative, reader.TemporalityPreference);
    }

    /// <summary>
    /// Ensures defaults also apply when threading overrides use tasks.
    /// </summary>
    [Fact]
    public void CreatePeriodicExportingMetricReader_Defaults_WithTask()
    {
        using var threadingOverride = ThreadingHelper.BeginThreadingOverride(isThreadingDisabled: true);

#pragma warning disable CA2000 // Dispose objects before losing scope
        var reader = CreatePeriodicExportingMetricReader();
#pragma warning restore CA2000 // Dispose objects before losing scope

        Assert.Equal(60000, reader.ExportIntervalMilliseconds);
        Assert.Equal(30000, reader.ExportTimeoutMilliseconds);
        Assert.Equal(MetricReaderTemporalityPreference.Cumulative, reader.TemporalityPreference);
    }

    /// <summary>
    /// Confirms the temporality preference is honored when specified via options.
    /// </summary>
    [Fact]
    public void CreatePeriodicExportingMetricReader_TemporalityPreference_FromOptions()
    {
        var value = MetricReaderTemporalityPreference.Delta;
        using var reader = CreatePeriodicExportingMetricReader(new()
        {
            TemporalityPreference = value,
        });

        Assert.Equal(value, reader.TemporalityPreference);
    }

    /// <summary>
    /// Verifies export interval options override environment variables.
    /// </summary>
    [Fact]
    public void CreatePeriodicExportingMetricReader_ExportIntervalMilliseconds_FromOptions()
    {
        Environment.SetEnvironmentVariable(PeriodicExportingMetricReaderOptions.OTelMetricExportIntervalEnvVarKey, "88888"); // should be ignored, as value set via options has higher priority
        var value = 123;
        using var reader = CreatePeriodicExportingMetricReader(new()
        {
            PeriodicExportingMetricReaderOptions = new()
            {
                ExportIntervalMilliseconds = value,
            },
        });

        Assert.Equal(value, reader.ExportIntervalMilliseconds);
    }

    /// <summary>
    /// Verifies export timeout options override environment variables.
    /// </summary>
    [Fact]
    public void CreatePeriodicExportingMetricReader_ExportTimeoutMilliseconds_FromOptions()
    {
        Environment.SetEnvironmentVariable(PeriodicExportingMetricReaderOptions.OTelMetricExportTimeoutEnvVarKey, "99999"); // should be ignored, as value set via options has higher priority
        var value = 456;
        using var reader = CreatePeriodicExportingMetricReader(new()
        {
            PeriodicExportingMetricReaderOptions = new()
            {
                ExportTimeoutMilliseconds = value,
            },
        });

        Assert.Equal(value, reader.ExportTimeoutMilliseconds);
    }

    /// <summary>
    /// Confirms the export interval pulls from environment variables when no options exist.
    /// </summary>
    [Fact]
    public void CreatePeriodicExportingMetricReader_ExportIntervalMilliseconds_FromEnvVar()
    {
        var value = 789;
        Environment.SetEnvironmentVariable(PeriodicExportingMetricReaderOptions.OTelMetricExportIntervalEnvVarKey, value.ToString(CultureInfo.InvariantCulture));
        using var reader = CreatePeriodicExportingMetricReader();

        Assert.Equal(value, reader.ExportIntervalMilliseconds);
    }

    /// <summary>
    /// Confirms the export timeout pulls from environment variables when no options exist.
    /// </summary>
    [Fact]
    public void CreatePeriodicExportingMetricReader_ExportTimeoutMilliseconds_FromEnvVar()
    {
        var value = 246;
        Environment.SetEnvironmentVariable(PeriodicExportingMetricReaderOptions.OTelMetricExportTimeoutEnvVarKey, value.ToString(CultureInfo.InvariantCulture));
        using var reader = CreatePeriodicExportingMetricReader();

        Assert.Equal(value, reader.ExportTimeoutMilliseconds);
    }

    /// <summary>
    /// Verifies configuration-based options load expected values.
    /// </summary>
    [Fact]
    public void CreatePeriodicExportingMetricReader_FromIConfiguration()
    {
        var values = new Dictionary<string, string?>()
        {
            [PeriodicExportingMetricReaderOptions.OTelMetricExportIntervalEnvVarKey] = "18",
            [PeriodicExportingMetricReaderOptions.OTelMetricExportTimeoutEnvVarKey] = "19",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var options = new PeriodicExportingMetricReaderOptions(configuration);

        Assert.Equal(18, options.ExportIntervalMilliseconds);
        Assert.Equal(19, options.ExportTimeoutMilliseconds);
    }

    /// <summary>
    /// Makes sure the environment variable names match the specification.
    /// </summary>
    [Fact]
    public void EnvironmentVariableNames()
    {
        Assert.Equal("OTEL_METRIC_EXPORT_INTERVAL", PeriodicExportingMetricReaderOptions.OTelMetricExportIntervalEnvVarKey);
        Assert.Equal("OTEL_METRIC_EXPORT_TIMEOUT", PeriodicExportingMetricReaderOptions.OTelMetricExportTimeoutEnvVarKey);
    }

    /// <summary>
    /// Clears environment variables set by tests to avoid bleed-over.
    /// </summary>
    private static void ClearEnvVars()
    {
        Environment.SetEnvironmentVariable(PeriodicExportingMetricReaderOptions.OTelMetricExportIntervalEnvVarKey, null);
        Environment.SetEnvironmentVariable(PeriodicExportingMetricReaderOptions.OTelMetricExportTimeoutEnvVarKey, null);
    }

    /// <summary>
    /// Creates a metric reader configured with test-specific options and exporter.
    /// </summary>
    /// <param name="options">Optional metric reader options to supply.</param>
    /// <returns>A configured <see cref="PeriodicExportingMetricReader"/> instance.</returns>
    private static PeriodicExportingMetricReader CreatePeriodicExportingMetricReader(
        MetricReaderOptions? options = null)
    {
        options ??= new();

#pragma warning disable CA2000 // Dispose objects before losing scope
        var dummyMetricExporter = new InMemoryExporter<Metric>(Array.Empty<Metric>());
#pragma warning restore CA2000 // Dispose objects before losing scope
        return PeriodicExportingMetricReaderHelper.CreatePeriodicExportingMetricReader(dummyMetricExporter, options);
    }
}
