using System.Xml;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>
/// Un résolveur qui ne répond que depuis un ensemble de documents déjà vérifiés.
/// </summary>
/// <remarks>
/// La différence avec un résolveur d'URL ordinaire est tout l'intérêt : celui-ci ne va rien chercher. Il
/// répond avec les octets déjà contrôlés aux emplacements autorisés, et rend <see langword="null"/> pour tout le
/// reste — si bien qu'un <c>schemaLocation</c> ajouté entre la vérification et la compilation ne peut rien
/// atteindre.
/// </remarks>
internal sealed class ConfinedResolver : XmlResolver
{
    private readonly IReadOnlyDictionary<string, string> _allowed;
    private readonly IReadOnlyDictionary<string, byte[]> _contents;

    /// <summary>Crée le résolveur.</summary>
    /// <param name="allowed">Les emplacements autorisés, et le fichier qui répond pour chacun.</param>
    /// <param name="contents">Le contenu déjà lu sous le budget et la politique de confinement.</param>
    internal ConfinedResolver(IReadOnlyDictionary<string, string> allowed, IReadOnlyDictionary<string, byte[]> contents)
    {
        _allowed = allowed;
        _contents = contents;
    }

    /// <inheritdoc />
    public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
    {
        ArgumentNullException.ThrowIfNull(absoluteUri);

        if (!_allowed.TryGetValue(absoluteUri.ToString(), out string? path)
            || !_contents.TryGetValue(path, out byte[]? content))
        {
            // Rien plutôt qu'une exception : le compilateur signalera l'absence, et le refus qui l'a
            // causée est déjà consigné avec sa raison. Lever ici remplacerait un diagnostic nommé par une
            // pile d'appels.
            return null;
        }

        return new MemoryStream(content, writable: false);
    }

    /// <inheritdoc />
    public override Uri ResolveUri(Uri? baseUri, string? relativeUri)
    {
        Uri resolved = base.ResolveUri(baseUri, relativeUri);
        // All aliases of a validated snapshot share one identity in XmlSchemaSet.
        return _allowed.TryGetValue(resolved.ToString(), out string? path) && _contents.ContainsKey(path)
            ? new Uri(Path.GetFullPath(path)) : resolved;
    }
}
