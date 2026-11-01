using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>Valide une action sans la modifier ni dévoiler une URL sensible dans les diagnostics.</summary>
internal static class SoapActionPolicy
{
    internal static void Validate(string? action)
    {
        if (string.IsNullOrEmpty(action))
        {
            return;
        }

        // The original dispatch value must be preserved. Stripping an authority or
        // query would change the SOAP contract; all authorities are refused because
        // we cannot establish which are public. Escaped values are refused as well,
        // instead of decoding and publishing a different action or a disguised URL.
        if (action.Any(character => char.IsWhiteSpace(character) || char.IsControl(character))
            || action.IndexOfAny(['?', '#', '@', '\\', '%', '"', '<', '>']) >= 0
            || action.Contains("//", StringComparison.Ordinal)
            || !Uri.TryCreate(action, UriKind.RelativeOrAbsolute, out Uri? uri)
            || (uri.IsAbsoluteUri && !string.Equals(uri.Scheme, "urn", StringComparison.Ordinal)))
        {
            throw new WsdlExportException("SOAP_ACTION_UNSAFE");
        }
    }
}
