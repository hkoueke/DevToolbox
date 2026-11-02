using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>Collecte vérifiable : originaux intacts, copies autonomes et manifeste sans URL d'origine.</summary>
internal static class WsdlExportSnapshot
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string[] Checks = ["Structure and semantic rules validated", "Serialized YAML reparsed and validated"];
    private static readonly string[] RemainingAcceptance = ["Import into the target Sparky instance", "Verify XML anyOf and SOAP Fault rendering", "Configure the service authority in the target"];

    internal static async Task<string> CompleteAsync(CollectedWsdlGraph contract, string directory,
        string title, string version, WsdlLimits limits, CancellationToken cancellationToken)
    {
        string originals = Path.Combine(directory, "sources");
        string offline = Path.Combine(directory, "offline");
        Directory.CreateDirectory(originals);
        Directory.CreateDirectory(offline);
        Dictionary<string, string> names = new(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        for (int index = 0; index < contract.Documents.Count; index++)
        {
            names.Add(Path.GetFullPath(contract.Documents[index].Path),
                "document-" + index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + ".xml");
        }

        List<SnapshotDocument> documents = [];
        long copiedBytes = 0;
        foreach (MaterialisedDocument document in contract.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ImportConfinement.Contains(contract.ConfinementRoot, document.Path))
            {
                throw new WsdlExportException("SNAPSHOT_INVALID");
            }

            byte[] bytes = BoundedDocumentReader.Read(document.Path, WsdlLimits.Default.MaxDocumentBytes)
                ?? throw new WsdlExportException("DOCUMENT_BYTE_LIMIT");
            if (!string.Equals(Hash(bytes), document.ContentHash, StringComparison.Ordinal))
            {
                throw new WsdlExportException("SNAPSHOT_CHANGED");
            }

            string name = names[Path.GetFullPath(document.Path)];
            await File.WriteAllBytesAsync(Path.Combine(originals, name), bytes, cancellationToken).ConfigureAwait(false);
            Uri origin = contract.Retrieval.Origin(document.Path) ?? throw new WsdlExportException("SNAPSHOT_INVALID");
            byte[] rewritten = Rewrite(bytes, origin, contract.Retrieval, names);
            copiedBytes += bytes.LongLength + rewritten.LongLength;
            if (copiedBytes > 2 * WsdlLimits.Default.MaxGraphBytes)
            {
                throw new WsdlExportException("DOCUMENT_BYTE_LIMIT");
            }

            await File.WriteAllBytesAsync(Path.Combine(offline, name), rewritten, cancellationToken).ConfigureAwait(false);
            documents.Add(new SnapshotDocument(name, Hash(bytes), Hash(rewritten)));
        }

        string entry = names[Path.GetFullPath(contract.Entry.Path)];
        string yaml = await SoapOpenApiYamlExporter.ExportFileAsync(Path.Combine(offline, entry),
            title, version, cancellationToken, limits).ConfigureAwait(false);
        byte[] output = Encoding.UTF8.GetBytes(yaml);
        if (output.LongLength > WsdlLimits.Default.MaxGraphBytes)
        {
            throw new WsdlExportException("DOCUMENT_BYTE_LIMIT");
        }

        await File.WriteAllBytesAsync(Path.Combine(directory, "openapi.yaml"), output, cancellationToken).ConfigureAwait(false);
        SnapshotManifest manifest = new(1, entry, title, version, documents, Hash(output));
        await File.WriteAllTextAsync(Path.Combine(directory, "manifest.json"),
            JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken).ConfigureAwait(false);
        await WriteProofAsync(directory, output, cancellationToken).ConfigureAwait(false);
        return yaml;
    }

    private static Task WriteProofAsync(string directory, byte[] output, CancellationToken cancellationToken) =>
        File.WriteAllTextAsync(Path.Combine(directory, "compatibility.json"),
            JsonSerializer.Serialize(new
            {
                Format = "OpenAPI 3.0.3",
                YamlSha256 = Hash(output),
                Validator = "Microsoft.OpenApi",
                Checks,
                SparkyImport = "Not tested: no real target configured",
                RemainingAcceptance,
            }, JsonOptions), cancellationToken);

    private static byte[] Rewrite(byte[] bytes, Uri origin, RetrievalMap retrieval, Dictionary<string, string> names)
    {
        using XmlReader reader = XmlReader.Create(new MemoryStream(bytes), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = WsdlLimits.Default.MaxDocumentBytes,
        });
        XElement root = XElement.Load(reader);
        foreach (XElement element in root.Descendants().Where(IsImport))
        {
            XAttribute? attribute = string.Equals(element.Name.NamespaceName, WsdlNamespaces.Wsdl11, StringComparison.Ordinal)
                ? element.Attribute("location") : element.Attribute("schemaLocation");
            if (attribute is null || string.IsNullOrWhiteSpace(attribute.Value))
            {
                continue;
            }

            if (!Uri.TryCreate(origin, attribute.Value, out Uri? designated)
                || retrieval.Local(designated) is not { } local
                || !names.TryGetValue(Path.GetFullPath(local), out string? target))
            {
                throw new WsdlExportException("SNAPSHOT_UNRESOLVED_IMPORT");
            }

            attribute.Value = target;
        }

        return Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));
    }

    internal static async Task<string> ReplayAsync(string manifestPath, string title, string version,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string directory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        byte[] bytes = ReadChecked(directory, "manifest.json", 1024 * 1024);
        SnapshotManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<SnapshotManifest>(bytes) ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new WsdlExportException("SNAPSHOT_INVALID");
        }

        if (manifest.FormatVersion != 1 || manifest.Documents is null || manifest.Documents.Count == 0
            || manifest.Documents.Count > WsdlLimits.Default.MaxDocumentCount
            || manifest.Documents.Any(document => document is null)
            || manifest.Documents.Select(document => document.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Documents.Count
            || !manifest.Documents.Any(document => string.Equals(document.Name, manifest.Entry, StringComparison.Ordinal)))
        {
            throw new WsdlExportException("SNAPSHOT_INVALID");
        }

        long total = 0;
        foreach (SnapshotDocument document in manifest.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] source = ReadChecked(Path.Combine(directory, "sources"), document.Name, WsdlLimits.Default.MaxDocumentBytes);
            byte[] offline = ReadChecked(Path.Combine(directory, "offline"), document.Name, WsdlLimits.Default.MaxDocumentBytes);
            total += source.LongLength + offline.LongLength;
            if (total > 2 * WsdlLimits.Default.MaxGraphBytes
                || !string.Equals(Hash(source), document.SourceSha256, StringComparison.Ordinal)
                || !string.Equals(Hash(offline), document.OfflineSha256, StringComparison.Ordinal))
            {
                throw new WsdlExportException("SNAPSHOT_CHANGED");
            }
        }

        byte[] savedYaml = ReadChecked(directory, "openapi.yaml", WsdlLimits.Default.MaxGraphBytes);
        if (!string.Equals(Hash(savedYaml), manifest.YamlSha256, StringComparison.Ordinal))
        {
            throw new WsdlExportException("SNAPSHOT_CHANGED");
        }

        string yaml = await SoapOpenApiYamlExporter.ExportFileAsync(Path.Combine(directory, "offline", manifest.Entry),
            title, version, cancellationToken).ConfigureAwait(false);
        if (string.Equals(title, manifest.Title, StringComparison.Ordinal)
            && string.Equals(version, manifest.ServiceVersion, StringComparison.Ordinal)
            && !string.Equals(Hash(Encoding.UTF8.GetBytes(yaml)), manifest.YamlSha256, StringComparison.Ordinal))
        {
            throw new WsdlExportException("SNAPSHOT_CHANGED");
        }

        return yaml;
    }

    private static byte[] ReadChecked(string directory, string name, long maximum)
    {
        if (string.IsNullOrWhiteSpace(name) || !string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal)
            || name is "." or ".." || !ImportConfinement.Contains(directory, Path.Combine(directory, name)))
        {
            throw new WsdlExportException("SNAPSHOT_INVALID");
        }

        return BoundedDocumentReader.Read(Path.Combine(directory, name), maximum)
            ?? throw new WsdlExportException("DOCUMENT_BYTE_LIMIT");
    }

    private static bool IsImport(XElement element) =>
        (string.Equals(element.Name.NamespaceName, WsdlNamespaces.Schema, StringComparison.Ordinal) && element.Name.LocalName is "import" or "include" or "redefine")
        || (string.Equals(element.Name.NamespaceName, WsdlNamespaces.Wsdl11, StringComparison.Ordinal) && string.Equals(element.Name.LocalName, "import", StringComparison.Ordinal));

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    internal sealed record SnapshotDocument(string Name, string SourceSha256, string OfflineSha256);
    internal sealed record SnapshotManifest(int FormatVersion, string Entry, string Title, string ServiceVersion,
        IReadOnlyList<SnapshotDocument> Documents, string YamlSha256);
}
