using DevToolbox.Domain.AzureDevOps;
using Spectre.Console;

namespace DevToolbox.Presentation.Shell;

/// <summary>
/// Demande l'adresse du serveur Azure DevOps au premier démarrage et retient la réponse.
/// </summary>
/// <remarks>
/// <para>
/// La valeur livrée dans <c>appsettings.json</c> est une suggestion, pas un réglage qu'un développeur est
/// censé modifier avant la première utilisation : elle est proposée par défaut, si bien que l'accepter tient
/// à une touche. Ce qui est confirmé est écrit dans le fichier de réglages propre à l'utilisateur, puis
/// réutilisé à chaque démarrage suivant.
/// </para>
/// <para>
/// La saisie attendue est celle que le développeur écrit tous les jours : un nom court tel que <c>azure</c>
/// ou <c>azure/</c> suffit, le schéma étant complété par <see cref="ServerAddress"/>. Exiger la forme
/// canonique reviendrait à refuser l'usage de la maison.
/// </para>
/// <para>
/// Cette invite saisit une adresse de serveur. Ce n'est pas, et ne doit jamais devenir, une demande
/// d'identifiants : il n'y a dans ce parcours ni mot de passe, ni jeton, ni nom d'utilisateur.
/// </para>
/// </remarks>
public sealed class FirstRunConfigurationPrompt
{
    private readonly IAnsiConsole _console;

    /// <summary>Crée l'invite.</summary>
    /// <param name="console">La console où présenter l'invite.</param>
    public FirstRunConfigurationPrompt(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <summary>Demande l'URL de base et la collection, en validant chaque réponse.</summary>
    /// <param name="suggestedBaseUrl">La suggestion issue d'<c>appsettings.json</c>, proposée par défaut.</param>
    /// <param name="suggestedCollection">La suggestion de collection, proposée par défaut.</param>
    /// <returns>L'adresse confirmée, sous sa forme normalisée.</returns>
    public ServerConfigurationAnswer Ask(string suggestedBaseUrl, string suggestedCollection)
    {
        _console.Write(new Rule("[bold]Premier démarrage — où se trouve votre serveur Azure DevOps ?[/]")
            .LeftJustified());
        _console.MarkupLine(
            "[grey]DevToolbox s'authentifie avec votre session Windows : il ne demande donc ni mot de passe "
            + "ni jeton. Il a seulement besoin de savoir quel serveur lire.[/]");
        _console.MarkupLine(
            "[grey]Le nom court du serveur suffit, par exemple [bold]azure[/] : le préfixe https:// est "
            + "ajouté pour vous.[/]");
        _console.WriteLine();

        string baseUrl = _console.Prompt(
            new TextPrompt<string>("Adresse du serveur :")
                .DefaultValue(suggestedBaseUrl)
                .Validate(ValidateBaseUrl));

        string collection = _console.Prompt(
            new TextPrompt<string>("Collection :")
                .DefaultValue(suggestedCollection)
                .Validate(ValidateCollection));

        _console.WriteLine();

        return new ServerConfigurationAnswer(Normalise(baseUrl), collection.Trim());
    }

    /// <summary>Signale qu'une adresse enregistrée n'est plus utilisable, avant de redemander.</summary>
    /// <param name="reason">Pourquoi la valeur enregistrée a été rejetée.</param>
    public void ReportSavedValueRejected(string reason)
    {
        _console.MarkupLine(
            "[yellow]L'adresse de serveur enregistrée n'est plus utilisable : "
            + Markup.Escape(reason) + "[/]");
        _console.WriteLine();
    }

    /// <summary>
    /// La forme retenue d'une adresse déjà validée. Ce qui est enregistré est donc toujours canonique, quelle
    /// que soit la manière dont il a été saisi.
    /// </summary>
    /// <param name="value">L'adresse saisie.</param>
    /// <returns>L'adresse normalisée.</returns>
    internal static string Normalise(string value) =>
        ServerAddress.TryNormalise(value, allowInsecureHttp: false, out Uri? address, out _)
            ? address!.AbsoluteUri
            : value.Trim();

    /// <summary>Le motif de rejet d'une adresse, dit dans les termes de celui qui vient de la saisir.</summary>
    /// <param name="problem">Ce qui empêche la saisie d'être retenue.</param>
    /// <returns>Une phrase à afficher.</returns>
    internal static string Explain(ServerAddressProblem problem) => problem switch
    {
        ServerAddressProblem.Empty =>
            "Saisissez l'adresse du serveur, par exemple azure ou https://devops.entreprise.local",

        ServerAddressProblem.MissingHost or ServerAddressProblem.Malformed =>
            "Cette saisie ne se lit pas comme une adresse de serveur.",

        // Le HTTP simple reste possible dans la configuration, mais volontairement pas ici : l'autoriser
        // doit demander une modification délibérée, pas une frappe au clavier devant une invite.
        ServerAddressProblem.InsecureScheme =>
            "L'adresse doit utiliser HTTPS.",

        ServerAddressProblem.UnsupportedScheme =>
            "Seules les adresses https:// sont acceptées ici.",

        ServerAddressProblem.CarriesCredentials =>
            "Une adresse de serveur ne porte pas d'identifiants : DevToolbox utilise votre session Windows.",

        _ => "Cette adresse ne peut pas être utilisée.",
    };

    private static ValidationResult ValidateBaseUrl(string value) =>
        ServerAddress.TryNormalise(value, allowInsecureHttp: false, out _, out ServerAddressProblem problem)
            ? ValidationResult.Success()
            : ValidationResult.Error("[red]" + Markup.Escape(Explain(problem)) + "[/]");

    private static ValidationResult ValidateCollection(string value)
    {
        string candidate = value.Trim();

        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > ServerTarget.MaxCollectionLength)
        {
            return ValidationResult.Error("[red]Saisissez un nom de collection de 1 à 64 caractères.[/]");
        }

        // La collection devient un segment d'URL : refuser tout ce qui pourrait en changer le sens.
        return ServerTarget.IsSafeCollection(candidate)
            ? ValidationResult.Success()
            : ValidationResult.Error(
                "[red]Un nom de collection ne peut contenir ni séparateur de chemin ni « .. ».[/]");
    }
}
