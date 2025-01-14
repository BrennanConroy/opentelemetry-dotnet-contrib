// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if NET || NETSTANDARD2_1
using System.Diagnostics.CodeAnalysis;
#endif
using System.Globalization;

namespace Microsoft.Extensions.Configuration;

internal static class OpenTelemetryConfigurationExtensions
{
    public delegate bool TryParseFunc<T>(
        string value,
#if NET || NETSTANDARD2_1
        [NotNullWhen(true)]
#endif
        out T? parsedValue);

    public static bool TryGetStringValue(
        this IConfiguration configuration,
        string key,
#if NET || NETSTANDARD2_1
        [NotNullWhen(true)]
#endif
        out string? value)
    {
        Debug.Assert(configuration != null, "configuration was null");

        value = configuration![key];

        return !string.IsNullOrWhiteSpace(value);
    }

    public static bool TryGetUriValue(
        this IConfiguration configuration,
        IConfigurationExtensionsLogger logger,
        string key,
#if NET || NETSTANDARD2_1
        [NotNullWhen(true)]
#endif
        out Uri? value)
    {
        Debug.Assert(logger != null, "logger was null");

        if (!configuration.TryGetStringValue(key, out var stringValue))
        {
            value = null;
            return false;
        }

        if (!Uri.TryCreate(stringValue, UriKind.Absolute, out value))
        {
            logger!.LogInvalidConfigurationValue(key, stringValue!);
            return false;
        }

        return true;
    }

    public static bool TryGetIntValue(
        this IConfiguration configuration,
        IConfigurationExtensionsLogger logger,
        string key,
        out int value)
    {
        Debug.Assert(logger != null, "logger was null");

        if (!configuration.TryGetStringValue(key, out var stringValue))
        {
            value = default;
            return false;
        }

        if (!int.TryParse(stringValue, NumberStyles.None, CultureInfo.InvariantCulture, out value))
        {
            logger!.LogInvalidConfigurationValue(key, stringValue!);
            return false;
        }

        return true;
    }

    public static bool TryGetBoolValue(
        this IConfiguration configuration,
        IConfigurationExtensionsLogger logger,
        string key,
        out bool value)
    {
        Debug.Assert(logger != null, "logger was null");

        if (!configuration.TryGetStringValue(key, out var stringValue))
        {
            value = default;
            return false;
        }

        if (!bool.TryParse(stringValue, out value))
        {
            logger!.LogInvalidConfigurationValue(key, stringValue!);
            return false;
        }

        return true;
    }

    public static bool TryGetValue<T>(
        this IConfiguration configuration,
        IConfigurationExtensionsLogger logger,
        string key,
        TryParseFunc<T> tryParseFunc,
#if NET || NETSTANDARD2_1
        [NotNullWhen(true)]
#endif
        out T? value)
    {
        Debug.Assert(logger != null, "logger was null");

        if (!configuration.TryGetStringValue(key, out var stringValue))
        {
            value = default;
            return false;
        }

        if (!tryParseFunc(stringValue!, out value))
        {
            logger!.LogInvalidConfigurationValue(key, stringValue!);
            return false;
        }

        return true;
    }
}
