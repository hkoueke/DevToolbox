using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Yaml;

/// <summary>
/// Signale qu'une limite de lecture a été atteinte, en nommant laquelle et où.
/// </summary>
/// <remarks>
/// Rattrapée par le lecteur, qui la convertit en diagnostic situé : elle ne franchit jamais la frontière
/// de la couche. Elle existe parce qu'une limite se découvre au fond d'une récursion, d'où remonter un
/// résultat demanderait de propager un échec à travers chaque niveau de la construction de l'arbre.
/// </remarks>
public sealed class LoadLimitReachedException : Exception
{
    /// <summary>Crée l'exception avec l'endroit où la limite a été atteinte.</summary>
    /// <param name="message">Quelle limite, et quelle était sa valeur.</param>
    /// <param name="position">Où la lecture en était.</param>
    /// <param name="location">Quel nœud était en cours de construction.</param>
    public LoadLimitReachedException(string message, SourcePosition? position, SpecPointer? location)
        : base(message)
    {
        Position = position;
        Location = location;
    }

    /// <summary>Crée l'exception sans message, pour satisfaire la forme attendue d'une exception.</summary>
    public LoadLimitReachedException()
    {
    }

    /// <summary>Crée l'exception avec un message.</summary>
    /// <param name="message">Quelle limite, et quelle était sa valeur.</param>
    public LoadLimitReachedException(string message)
        : base(message)
    {
    }

    /// <summary>Crée l'exception avec un message et sa cause.</summary>
    /// <param name="message">Quelle limite, et quelle était sa valeur.</param>
    /// <param name="innerException">Ce qui l'a provoquée.</param>
    public LoadLimitReachedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Où la lecture en était, quand on le sait.</summary>
    public SourcePosition? Position { get; }

    /// <summary>Quel nœud était en cours de construction, quand on le sait.</summary>
    public SpecPointer? Location { get; }
}
