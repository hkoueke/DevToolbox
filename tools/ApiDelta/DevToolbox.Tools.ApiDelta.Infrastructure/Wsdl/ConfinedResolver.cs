using System.Xml;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;

/// <summary>
/// Un résolveur qui ne répond que depuis un ensemble de documents déjà vérifiés.
/// </summary>
/// <remarks>
/// La différence avec un résolveur d'URL ordinaire est tout l'intérêt : celui-ci ne va rien chercher. Il
/// répond aux emplacements que la politique a déjà autorisés, et rend <see langword="null"/> pour tout le
/// reste — si bien qu'un <c>schemaLocation</c> ajouté entre la vérification et la compilation ne peut rien
/// atteindre.
/// </remarks>
internal sealed class ConfinedResolver : XmlResolver
{
    private readonly IReadOnlyDictionary<string, string> _allowed;

    /// <summary>Crée le résolveur.</summary>
    /// <param name="allowed">Les emplacements autorisés, et le fichier qui répond pour chacun.</param>
    internal ConfinedResolver(IReadOnlyDictionary<string, string> allowed) => _allowed = allowed;

    /// <inheritdoc />
    public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
    {
        ArgumentNullException.ThrowIfNull(absoluteUri);

        if (!_allowed.TryGetValue(absoluteUri.ToString(), out string? path))
        {
            // Rien plutôt qu'une exception : le compilateur signalera l'absence, et le refus qui l'a
            // causée est déjà consigné avec sa raison. Lever ici remplacerait un diagnostic nommé par une
            // pile d'appels.
            return null;
        }

        return File.OpenRead(path);
    }

    /// <inheritdoc />
    public override Uri ResolveUri(Uri? baseUri, string? relativeUri) =>
        base.ResolveUri(baseUri, relativeUri);
}
