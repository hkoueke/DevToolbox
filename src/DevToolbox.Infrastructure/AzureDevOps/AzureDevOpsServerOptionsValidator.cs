using DevToolbox.Domain.AzureDevOps;
using Microsoft.Extensions.Options;

namespace DevToolbox.Infrastructure.AzureDevOps;

/// <summary>
/// Valide les options du serveur au démarrage, pour qu'une installation mal configurée échoue de façon
/// lisible plutôt qu'à la première requête. Les messages nomment le réglage fautif et le fichier à corriger.
/// </summary>
/// <remarks>
/// L'adresse de base est jugée par <see cref="ServerAddress"/>, la même lecture que celle de l'invite du
/// premier démarrage. Un nom court tel que <c>azure</c> passe donc ici comme il y passe là-bas : deux
/// jugements divergents sur la même saisie seraient la pire des réponses.
/// </remarks>
public sealed class AzureDevOpsServerOptionsValidator : IValidateOptions<AzureDevOpsServerOptions>
{
    private readonly string _settingsPath;
    private readonly int _totalRequestTimeoutSeconds;

    /// <summary>Crée le validateur.</summary>
    /// <param name="settingsPath">Le fichier de réglages à nommer dans les messages d'échec.</param>
    /// <param name="totalRequestTimeoutSeconds">
    /// Le budget total d'une requête logique, tel que le pipeline de résilience l'applique. Le délai du
    /// client HTTP doit rester au-dessus, sans quoi les deux échéances courent l'une contre l'autre.
    /// </param>
    public AzureDevOpsServerOptionsValidator(string settingsPath, int totalRequestTimeoutSeconds)
    {
        _settingsPath = settingsPath;
        _totalRequestTimeoutSeconds = totalRequestTimeoutSeconds;
    }

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AzureDevOpsServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (!ServerAddress.TryNormalise(
                options.BaseUrl, options.AllowInsecureHttp, out _, out ServerAddressProblem problem))
        {
            failures.Add(problem == ServerAddressProblem.Empty
                ? Missing(nameof(options.BaseUrl), "par exemple azure ou https://devops.entreprise.local")
                : Invalid(nameof(options.BaseUrl), options.BaseUrl, Explain(problem)));
        }

        if (string.IsNullOrWhiteSpace(options.Collection))
        {
            failures.Add(Missing(nameof(options.Collection), "par exemple DefaultCollection"));
        }
        else if (!ServerTarget.IsSafeCollection(options.Collection))
        {
            failures.Add(Invalid(
                nameof(options.Collection),
                options.Collection,
                "il ne doit contenir ni séparateur de chemin, ni marqueur de dossier parent, ni caractère "
                + "de contrôle"));
        }

        if (string.IsNullOrWhiteSpace(options.ApiVersion))
        {
            failures.Add(Missing(nameof(options.ApiVersion), "par exemple 7.1"));
        }

        if (options.HttpTimeoutSeconds <= _totalRequestTimeoutSeconds)
        {
            // Le délai du client HTTP couvre aussi les attentes entre reprises. S'il n'excède pas le budget
            // du pipeline, les deux échéances expirent ensemble et laquelle l'emporte n'est pas décidable :
            // tantôt un échec propre, tantôt une exception qui traverse tout.
            failures.Add(
                $"AzureDevOpsServer:HttpTimeoutSeconds vaut {options.HttpTimeoutSeconds} s, ce qui n'excède "
                + $"pas Resilience:TotalRequestTimeoutSeconds ({_totalRequestTimeoutSeconds} s). Le délai du "
                + "client doit rester le filet extérieur, franchement au-dessus du budget des reprises. "
                + $"Corrigez {_settingsPath}.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }


    private static string Explain(ServerAddressProblem problem) => problem switch
    {
        ServerAddressProblem.InsecureScheme =>
            "elle doit utiliser HTTPS. N'activez AllowInsecureHttp que si vous voulez réellement du HTTP "
            + "simple",

        ServerAddressProblem.UnsupportedScheme => "son schéma n'est ni https ni http",

        ServerAddressProblem.CarriesCredentials =>
            "elle porte des identifiants, ce qu'une adresse de serveur ne doit jamais faire",

        _ => "elle ne se lit pas comme une adresse de serveur",
    };

    private string Missing(string setting, string hint) =>
        $"AzureDevOpsServer:{setting} n'est pas renseigné ({hint}). Renseignez-le dans {_settingsPath}.";

    private string Invalid(string setting, string value, string why) =>
        $"AzureDevOpsServer:{setting} vaut « {value} », ce qui ne peut pas être utilisé car {why}. "
        + $"Corrigez {_settingsPath}.";
}
