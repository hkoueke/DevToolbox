namespace DevToolbox.Tools.ApiDelta.Core.Authoring;

/// <summary>Un champ de l'entrée d'une opération, et ce que le développeur en a décidé.</summary>
/// <param name="Field">Le nom du champ, tel que le contrat le déclare.</param>
/// <param name="Declared">Son type, tel que le contrat le déclare, pour qu'un refus puisse le nommer.</param>
/// <param name="IsComposite">
/// Vrai lorsque le type n'est pas un scalaire. Établi par la projection, non redéduit ici.
/// </param>
/// <param name="Location">Où le champ voyage.</param>
/// <param name="Required">Ce que le développeur a répondu. Forcé vrai dans le chemin.</param>
/// <param name="Name">
/// Le nom du paramètre lorsqu'il diffère de celui du champ — un en-tête, typiquement. Nul sinon.
/// </param>
/// <remarks>
/// Les invariants sont tenus ici et non par l'appelant. Un placement impossible ne doit pas pouvoir
/// exister en mémoire, parce qu'il finirait par être écrit : un paramètre de chemin facultatif donne un
/// document invalide, et un composite aplati en scalaires inventés donne un contrat qui décrit des champs
/// dont personne n'a parlé.
/// </remarks>
public sealed record FieldPlacement(
    string Field,
    string Declared,
    bool IsComposite,
    ParameterLocation Location,
    bool Required,
    string? Name = null)
{
    /// <summary>La destination, refusée si elle est impossible pour ce type.</summary>
    /// <remarks>
    /// La validation vit dans l'initialiseur parce qu'un enregistrement positionnel n'a pas de corps de
    /// constructeur. L'effet est le même et c'est ce qui compte : un placement impossible ne peut pas
    /// exister en mémoire, donc ne peut pas être écrit.
    /// </remarks>
    public ParameterLocation Location { get; init; } = Possible(Field, Declared, IsComposite, Location);

    /// <summary>Le caractère obligatoire, tel que le placement l'impose ou le laisse au développeur.</summary>
    /// <remarks>
    /// Un paramètre de chemin ne peut pas être facultatif, et un champ écarté ne peut pas être exigé. Les
    /// deux sont corrigés plutôt que refusés : ce ne sont pas des erreurs de l'appelant mais des
    /// conséquences du placement, et les faire porter par le type évite de les redire partout.
    /// </remarks>
    public bool Required { get; init; } = Location switch
    {
        ParameterLocation.Path => true,
        ParameterLocation.Nowhere => false,
        _ => Required,
    };

    /// <summary>Le nom effectif du paramètre : celui demandé, ou celui du champ.</summary>
    public string Effective => Name is { Length: > 0 } named ? named : Field;

    /// <summary>Indique que le champ a été écarté sciemment.</summary>
    public bool IsDropped => Location is ParameterLocation.Nowhere;

    /// <summary>Ce que le placement dit, en une ligne.</summary>
    public string Display => Effective + " → " + Where(Location) + Obligation;

    /// <summary>Ce qu'il y a à dire du caractère obligatoire, s'il y a quelque chose à en dire.</summary>
    private string Obligation
    {
        get
        {
            if (IsDropped)
            {
                return string.Empty;
            }

            return Required ? ", obligatoire" : ", facultatif";
        }
    }

    /// <summary>Refuse un composite placé là où OpenAPI attend un scalaire.</summary>
    /// <exception cref="ArgumentException">
    /// Lorsque le type n'est pas un scalaire et que la destination en exige un. Le message nomme le champ
    /// et son type, parce qu'un refus qui ne nomme pas son sujet ne se corrige pas.
    /// </exception>
    private static ParameterLocation Possible(
        string field, string declared, bool composite, ParameterLocation location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(declared);

        return composite && location is ParameterLocation.Path or ParameterLocation.Query
            or ParameterLocation.Header
            ? throw new ArgumentException(
                "« " + field + " » est de type " + declared + ", qui n'est pas un scalaire : il ne peut "
                + "pas voyager dans le " + Where(location) + ".",
                nameof(location))
            : location;
    }

    /// <summary>La destination, dans les mots du développeur.</summary>
    /// <param name="location">La destination.</param>
    /// <returns>Son nom.</returns>
    public static string Where(ParameterLocation location) => location switch
    {
        ParameterLocation.Path => "chemin",
        ParameterLocation.Query => "requête",
        ParameterLocation.Body => "corps",
        ParameterLocation.Header => "en-tête",
        ParameterLocation.Nowhere => "écarté",
        _ => throw new ArgumentOutOfRangeException(nameof(location), location, "Destination inconnue."),
    };
}
