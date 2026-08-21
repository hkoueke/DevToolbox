using Microsoft.Extensions.Options;

namespace DevToolbox.Infrastructure.AzureDevOps;

/// <summary>
/// Valide les options du serveur au démarrage, pour qu'une installation mal configurée échoue de façon
/// lisible plutôt qu'à la première requête. Les messages nomment le réglage fautif et le fichier à corriger.
/// </summary>
public sealed class AzureDevOpsServerOptionsValidator : IValidateOptions<AzureDevOpsServerOptions>
{
    private readonly string _settingsPath;

    /// <summary>Crée le validateur.</summary>
    /// <param name="settingsPath">Le fichier de réglages à nommer dans les messages d'échec.</param>
    public AzureDevOpsServerOptionsValidator(string settingsPath) => _settingsPath = settingsPath;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AzureDevOpsServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            failures.Add(Missing(nameof(options.BaseUrl), "for example https://devops.entreprise.local"));
        }
        else if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out Uri? baseUri))
        {
            failures.Add(Invalid(nameof(options.BaseUrl), options.BaseUrl, "it is not an absolute URL"));
        }
        else if (!IsPermittedScheme(baseUri, options.AllowInsecureHttp))
        {
            failures.Add(Invalid(
                nameof(options.BaseUrl),
                options.BaseUrl,
                "it must use HTTPS. Set AllowInsecureHttp to true only if you genuinely intend plain HTTP"));
        }

        if (string.IsNullOrWhiteSpace(options.Collection))
        {
            failures.Add(Missing(nameof(options.Collection), "for example DefaultCollection"));
        }
        else if (!IsSafeSegment(options.Collection))
        {
            failures.Add(Invalid(
                nameof(options.Collection),
                options.Collection,
                "it must not contain a path separator, a parent-directory marker, or a control character"));
        }

        if (string.IsNullOrWhiteSpace(options.ApiVersion))
        {
            failures.Add(Missing(nameof(options.ApiVersion), "for example 7.1"));
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>Indique si un segment de chemin peut être placé sans risque dans une URL.</summary>
    /// <param name="segment">Le segment à tester.</param>
    /// <returns><see langword="true"/> si le segment est sûr.</returns>
    internal static bool IsSafeSegment(string segment) =>
        !string.IsNullOrWhiteSpace(segment)
        && segment.Length <= 64
        && !segment.Contains('/', StringComparison.Ordinal)
        && !segment.Contains('\\', StringComparison.Ordinal)
        && !segment.Contains("..", StringComparison.Ordinal)
        && !segment.Any(char.IsControl);

    private static bool IsPermittedScheme(Uri baseUri, bool allowInsecureHttp) =>
        baseUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
        || (allowInsecureHttp
            && baseUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase));

    private string Missing(string setting, string hint) =>
        $"AzureDevOpsServer:{setting} is not set ({hint}). Set it in {_settingsPath}.";

    private string Invalid(string setting, string value, string why) =>
        $"AzureDevOpsServer:{setting} is '{value}', which cannot be used because {why}. Edit {_settingsPath}.";
}
