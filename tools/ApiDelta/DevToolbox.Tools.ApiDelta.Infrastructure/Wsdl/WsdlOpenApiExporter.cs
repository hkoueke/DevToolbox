using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>Adaptateur d'export SOAP pour la présentation.</summary>
public sealed class WsdlOpenApiExporter(IContractFetcher? fetcher = null) : IWsdlOpenApiExporter
{
    /// <inheritdoc />
    public Task<string> ExportAsync(
        string wsdlPath, string title, string version, CancellationToken cancellationToken) =>
        string.Equals(Path.GetFileName(wsdlPath), "manifest.json", StringComparison.OrdinalIgnoreCase)
            ? WsdlExportSnapshot.ReplayAsync(wsdlPath, title, version, cancellationToken)
            : SoapOpenApiYamlExporter.ExportFileAsync(wsdlPath, title, version, cancellationToken);

    /// <inheritdoc />
    public async Task<string> ExportUrlAsync(Uri source, WsdlUrlExportOptions options,
        string title, string version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        if (fetcher is null || !source.IsAbsoluteUri || source.Scheme is not ("https" or "http"))
        {
            throw new WsdlExportException("COLLECTION_SOURCE_INVALID");
        }

        WsdlLimits limits = options.Limits ?? WsdlLimits.Default;
        if (limits.MaxDocumentBytes <= 0 || limits.MaxResponseBytes <= 0 || limits.MaxGraphBytes <= 0
            || limits.MaxDocumentCount <= 0 || limits.MaxImportChainDepth < 0 || limits.FetchTimeoutSeconds <= 0
            || limits.MaxNestingDepth <= 0 || limits.MaxDocumentBytes > WsdlLimits.Default.MaxDocumentBytes
            || limits.MaxEnumerationValues <= 0 || limits.MaxEnumerationValues > WsdlLimits.Default.MaxEnumerationValues
            || limits.MaxResponseBytes > WsdlLimits.Default.MaxResponseBytes || limits.FetchTimeoutSeconds > WsdlLimits.Default.FetchTimeoutSeconds
            || limits.MaxGraphBytes > WsdlLimits.Default.MaxGraphBytes || limits.MaxDocumentCount > WsdlLimits.Default.MaxDocumentCount
            || limits.MaxNestingDepth > WsdlLimits.Default.MaxNestingDepth || limits.MaxImportChainDepth > WsdlLimits.Default.MaxImportChainDepth)
        {
            throw new WsdlExportException("COLLECTION_LIMITS_INVALID");
        }

        string target = Path.GetFullPath(options.SnapshotDirectory);
        string parent = Path.GetDirectoryName(target) ?? throw new WsdlExportException("SNAPSHOT_INVALID");
        if (!Directory.Exists(parent) || Directory.Exists(target) || File.Exists(target)
            || !ImportConfinement.Contains(parent, target))
        {
            throw new WsdlExportException("COLLECTION_DESTINATION_EXISTS_OR_INVALID");
        }

        string staging = Path.Combine(parent, ".wsdl-" + Guid.NewGuid().ToString("N"));
        CreatePrivateDirectory(staging);
        try
        {
            string raw = Path.Combine(staging, "download");
            ImportPolicy policy = CreatePolicy(raw, source, options);
            var result = await fetcher.CollectAsync(source, raw, limits, policy, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                throw new WsdlExportException("COLLECTION_" + result.Failure!.Reason.ToString().ToUpperInvariant());
            }

            string yaml = await WsdlExportSnapshot.CompleteAsync(result.Value, staging, title, version, limits, cancellationToken)
                .ConfigureAwait(false);
            Directory.Delete(raw, recursive: true);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(staging, target);
            return yaml;
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
    }

    private static void CreatePrivateDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static ImportPolicy CreatePolicy(string directory, Uri source, WsdlUrlExportOptions options)
    {
        string[] hosts = [source.IdnHost, .. options.AdditionalHosts ?? []];
        if (hosts.Any(host => string.IsNullOrWhiteSpace(host) || Uri.CheckHostName(host) == UriHostNameType.Unknown))
        {
            throw new WsdlExportException("COLLECTION_HOST_INVALID");
        }

        string[] normalized = [.. hosts.Select(host => new UriBuilder("https", host).Uri.IdnHost).Distinct(StringComparer.OrdinalIgnoreCase)];
        List<string> addresses = [];
        foreach (string value in options.ConfirmedAddresses ?? [])
        {
            if (!System.Net.IPAddress.TryParse(value, out var address))
            {
                throw new WsdlExportException("COLLECTION_HOST_INVALID");
            }

            addresses.Add(address.ToString());
        }

        return new ImportPolicy(directory, AllowRemote: true, AllowedHosts: normalized,
            AllowPlainHttp: options.AllowPlainHttp, ConfirmedAddresses: addresses);
    }
}
