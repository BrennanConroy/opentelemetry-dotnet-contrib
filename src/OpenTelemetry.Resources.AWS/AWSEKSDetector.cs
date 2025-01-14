// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Text;
using OpenTelemetry.Resources.AWS.Models;

namespace OpenTelemetry.Resources.AWS;

/// <summary>
/// Resource detector for application running in AWS EKS.
/// </summary>
internal sealed class AWSEKSDetector : IResourceDetector
{
    private const string AWSEKSCertificatePath = "/var/run/secrets/kubernetes.io/serviceaccount/ca.crt";
    private const string AWSEKSCredentialPath = "/var/run/secrets/kubernetes.io/serviceaccount/token";
    private const string AWSEKSMetadataFilePath = "/proc/self/cgroup";
    private const string AWSClusterInfoUrl = "https://kubernetes.default.svc/api/v1/namespaces/amazon-cloudwatch/configmaps/cluster-info";
    private const string AWSAuthUrl = "https://kubernetes.default.svc/api/v1/namespaces/kube-system/configmaps/aws-auth";

    /// <summary>
    /// Detector the required and optional resource attributes from AWS EKS.
    /// </summary>
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        var credentials = GetEKSCredentials(AWSEKSCredentialPath);
        using var httpClientHandler = ServerCertificateValidationHandler.Create(AWSEKSCertificatePath, AWSResourcesEventSource.Log);

        return credentials == null || !IsEKSProcess(credentials, httpClientHandler)
            ? Resource.Empty
            : new Resource(ExtractResourceAttributes(
                GetEKSClusterName(credentials, httpClientHandler),
                GetEKSContainerId(AWSEKSMetadataFilePath)));
    }

    internal static List<KeyValuePair<string, object>> ExtractResourceAttributes(string? clusterName, string? containerId)
    {
        var resourceAttributes = new List<KeyValuePair<string, object>>()
        {
            new(AWSSemanticConventions.AttributeCloudProvider, AWSSemanticConventions.CloudProviderValuesAws),
            new(AWSSemanticConventions.AttributeCloudPlatform, AWSSemanticConventions.CloudPlatformValuesAwsEks),
        };

        if (!string.IsNullOrEmpty(clusterName))
        {
            resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeK8SClusterName, clusterName!));
        }

        if (!string.IsNullOrEmpty(containerId))
        {
            resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeContainerID, containerId!));
        }

        return resourceAttributes;
    }

    internal static string? GetEKSCredentials(string path)
    {
        try
        {
            var stringBuilder = new StringBuilder("Bearer ");

            using (var streamReader = ResourceDetectorUtils.GetStreamReader(path))
            {
                while (!streamReader.EndOfStream)
                {
                    stringBuilder.Append(streamReader.ReadLine()?.Trim());
                }
            }

            return stringBuilder.ToString();
        }
        catch (Exception ex)
        {
            AWSResourcesEventSource.Log.ResourceAttributesExtractException($"{nameof(AWSEKSDetector)} : Failed to load client token", ex);
        }

        return null;
    }

    internal static string? GetEKSContainerId(string path)
    {
        try
        {
            using var streamReader = ResourceDetectorUtils.GetStreamReader(path);
            while (!streamReader.EndOfStream)
            {
                var trimmedLine = streamReader.ReadLine()?.Trim();
                if (trimmedLine?.Length > 64)
                {
                    return trimmedLine.Substring(trimmedLine.Length - 64);
                }
            }
        }
        catch (Exception ex)
        {
            AWSResourcesEventSource.Log.ResourceAttributesExtractException($"{nameof(AWSEKSDetector)} : Failed to get Container Id", ex);
        }

        return null;
    }

    internal static AWSEKSClusterInformationModel? DeserializeResponse(string response)
    {
#if NET
        return ResourceDetectorUtils.DeserializeFromString(response, SourceGenerationContext.Default.AWSEKSClusterInformationModel);
#else
        return ResourceDetectorUtils.DeserializeFromString<AWSEKSClusterInformationModel>(response);
#endif
    }

    private static string? GetEKSClusterName(string credentials, HttpClientHandler? httpClientHandler)
    {
        try
        {
            var clusterInfo = GetEKSClusterInfo(credentials, httpClientHandler);
            return DeserializeResponse(clusterInfo)?.Data?.ClusterName;
        }
        catch (Exception ex)
        {
            AWSResourcesEventSource.Log.ResourceAttributesExtractException($"{nameof(AWSEKSDetector)} : Failed to get cluster information", ex);
        }

        return null;
    }

    private static bool IsEKSProcess(string credentials, HttpClientHandler? httpClientHandler)
    {
        string? awsAuth = null;
        try
        {
            awsAuth = AsyncHelper.RunSync(() => ResourceDetectorUtils.SendOutRequestAsync(AWSAuthUrl, HttpMethod.Get, new KeyValuePair<string, string>("Authorization", credentials), httpClientHandler));
        }
        catch (Exception ex)
        {
            AWSResourcesEventSource.Log.ResourceAttributesExtractException($"{nameof(AWSEKSDetector)} : Failed to get EKS information", ex);
        }

        return !string.IsNullOrEmpty(awsAuth);
    }

    private static string GetEKSClusterInfo(string credentials, HttpClientHandler? httpClientHandler)
    {
        return AsyncHelper.RunSync(() => ResourceDetectorUtils.SendOutRequestAsync(AWSClusterInfoUrl, HttpMethod.Get, new KeyValuePair<string, string>("Authorization", credentials), httpClientHandler));
    }
}
#endif
