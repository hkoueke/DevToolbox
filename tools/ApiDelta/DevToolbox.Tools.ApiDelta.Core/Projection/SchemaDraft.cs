namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>Un schéma JSON en cours de construction, avant qu'il devienne un arbre de syntaxe.</summary>
/// <remarks>
/// Une forme intermédiaire mutable, et volontairement bête : un sac de mots-clés. Elle existe pour que les
/// règles de projection n'aient à tenir aucune comptabilité de pointeurs ni de positions — elles disent
/// « ce type a un « maxLength » de 8 » et rien de plus. La conversion en nœuds, avec ses pointeurs et son
/// absence d'étendue, se fait une seule fois, à la fin, au même endroit.
/// <para>
/// Les valeurs admises sont celles que JSON porte : une chaîne, un booléen, un entier, un flottant, une
/// liste, ou un autre brouillon. Rien d'autre, afin qu'aucune règle ne puisse y déposer quelque chose
/// qu'un document OpenAPI ne saurait exprimer.
/// </para>
/// </remarks>
public sealed class SchemaDraft
{
    private readonly Dictionary<string, object?> _keywords = new(StringComparer.Ordinal);

    /// <summary>Les mots-clés posés, dans l'ordre où ils l'ont été.</summary>
    public IReadOnlyDictionary<string, object?> Keywords => _keywords;

    /// <summary>Pose un mot-clé, ou le remplace.</summary>
    /// <param name="keyword">Son nom.</param>
    /// <param name="value">Sa valeur.</param>
    public void Set(string keyword, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyword);

        _keywords[keyword] = value;
    }

    /// <summary>Lit un mot-clé, s'il est posé.</summary>
    /// <param name="keyword">Son nom.</param>
    /// <returns>Sa valeur, ou <see langword="null"/>.</returns>
    public object? Get(string keyword) => _keywords.GetValueOrDefault(keyword);

    /// <summary>Ajoute une valeur à une liste, en la créant au besoin.</summary>
    /// <param name="keyword">Le nom de la liste.</param>
    /// <param name="value">La valeur à ajouter.</param>
    public void Append(string keyword, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyword);

        if (_keywords.GetValueOrDefault(keyword) is not List<object?> list)
        {
            list = [];
            _keywords[keyword] = list;
        }

        list.Add(value);
    }

    /// <summary>
    /// Le sous-brouillon posé sous une clé, créé s'il n'existe pas.
    /// </summary>
    /// <param name="name">La clé.</param>
    /// <returns>Son brouillon.</returns>
    /// <remarks>
    /// Distinct de <see cref="Property"/>, qui descend d'abord sous « properties ». La première version
    /// n'avait que le second, si bien que « paths » recevait un niveau « properties » parasite et que les
    /// réponses d'une opération se posaient à côté de « responses » plutôt que dedans. Les deux ont été
    /// vus en vidant le document produit.
    /// </remarks>
    public SchemaDraft Section(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (Get(name) is not SchemaDraft section)
        {
            section = new SchemaDraft();
            Set(name, section);
        }

        return section;
    }

    /// <summary>Le sous-brouillon d'une propriété d'objet, créé s'il n'existe pas.</summary>
    /// <param name="name">Le nom de la propriété.</param>
    /// <returns>Son brouillon.</returns>
    public SchemaDraft Property(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return Section("properties").Section(name);
    }
}
