namespace DevToolbox.Presentation.Shell;

/// <summary>
/// Les codes de sortie documentés. Ctrl+C renvoie un code non nul documenté plutôt qu'une valeur
/// arbitraire.
/// </summary>
public static class ExitCodes
{
    /// <summary>L'exécution s'est terminée normalement.</summary>
    public const int Success = 0;

    /// <summary>La configuration est absente ou invalide : l'application n'a pas pu démarrer.</summary>
    public const int InvalidConfiguration = 2;

    /// <summary>Le terminal n'est pas interactif : un outil piloté par menus ne peut pas fonctionner.</summary>
    public const int NotInteractive = 3;

    /// <summary>Le développeur a annulé avec Ctrl+C.</summary>
    public const int Cancelled = 130;
}
