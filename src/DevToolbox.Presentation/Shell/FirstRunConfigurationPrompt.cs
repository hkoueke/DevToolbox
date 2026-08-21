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
    /// <returns>L'adresse confirmée.</returns>
    public ServerConfigurationAnswer Ask(string suggestedBaseUrl, string suggestedCollection)
    {
        _console.Write(new Rule("[bold]First run — where is your Azure DevOps Server?[/]").LeftJustified());
        _console.MarkupLine(
            "[grey]DevToolbox signs in with your Windows session, so it never asks for a password or a "
            + "token. It only needs to know which server to read.[/]");
        _console.WriteLine();

        string baseUrl = _console.Prompt(
            new TextPrompt<string>("Server base URL:")
                .DefaultValue(suggestedBaseUrl)
                .Validate(ValidateBaseUrl));

        string collection = _console.Prompt(
            new TextPrompt<string>("Collection:")
                .DefaultValue(suggestedCollection)
                .Validate(ValidateCollection));

        _console.WriteLine();

        return new ServerConfigurationAnswer(baseUrl.Trim(), collection.Trim());
    }

    /// <summary>Signale qu'une adresse enregistrée n'est plus utilisable, avant de redemander.</summary>
    /// <param name="reason">Pourquoi la valeur enregistrée a été rejetée.</param>
    public void ReportSavedValueRejected(string reason)
    {
        _console.MarkupLine("[yellow]The saved server address cannot be used: " + Markup.Escape(reason) + "[/]");
        _console.WriteLine();
    }

    private static ValidationResult ValidateBaseUrl(string value)
    {
        string candidate = value.Trim();

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return ValidationResult.Error("[red]Enter the server address, for example https://devops.contoso.local[/]");
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri))
        {
            return ValidationResult.Error("[red]That is not an absolute URL.[/]");
        }

        // HTTPS uniquement. L'option permettant le HTTP simple existe dans la configuration mais n'est
        // volontairement pas proposée ici : l'autoriser doit demander une modification délibérée, pas une
        // frappe au clavier devant une invite.
        return uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? ValidationResult.Success()
            : ValidationResult.Error("[red]The address must use HTTPS.[/]");
    }

    private static ValidationResult ValidateCollection(string value)
    {
        string candidate = value.Trim();

        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 64)
        {
            return ValidationResult.Error("[red]Enter a collection name of 1 to 64 characters.[/]");
        }

        // La collection devient un segment d'URL : refuser tout ce qui pourrait en changer le sens.
        bool unsafeSegment = candidate.Contains('/', StringComparison.Ordinal)
            || candidate.Contains('\\', StringComparison.Ordinal)
            || candidate.Contains("..", StringComparison.Ordinal)
            || candidate.Any(char.IsControl);

        return unsafeSegment
            ? ValidationResult.Error("[red]A collection name cannot contain a path separator or '..'.[/]")
            : ValidationResult.Success();
    }
}
