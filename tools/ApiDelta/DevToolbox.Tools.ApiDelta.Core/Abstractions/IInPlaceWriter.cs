using DevToolbox.Domain.Results;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Réécrit la description de production sur place.</summary>
/// <remarks>
/// Un port distinct du magasin d'artefacts, et délibérément : le magasin est confiné sous le dossier de
/// sortie, ce qui est exactement ce qui le rend sans danger. L'écriture sur place sort de ce confinement,
/// donc elle ne doit pas pouvoir arriver par le même chemin — c'est le genre de distinction qu'un
/// paramètre booléen aurait effacée.
/// </remarks>
public interface IInPlaceWriter
{
    /// <summary>Réécrit la description de production.</summary>
    /// <param name="production">La description lue, dont l'empreinte sert de témoin.</param>
    /// <param name="content">Ce qu'il faut écrire.</param>
    /// <param name="backup">Crée une sauvegarde, sauf refus explicite.</param>
    /// <param name="cancellationToken">Annule l'écriture.</param>
    /// <returns>L'artefact écrit, ou de quoi expliquer le refus.</returns>
    Task<Result<Artifact>> WriteAsync(
        SpecDocument production,
        ReadOnlyMemory<byte> content,
        bool backup,
        CancellationToken cancellationToken);
}
