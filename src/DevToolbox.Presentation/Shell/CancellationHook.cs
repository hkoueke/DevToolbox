namespace DevToolbox.Presentation.Shell;

/// <summary>
/// Transforme Ctrl+C en annulation coopérative : le jeton se déclenche, le travail en cours s'annule, les
/// points de reprise sont vidés, les puits de journalisation sont libérés, et le processus renvoie un code
/// de sortie non nul documenté.
/// </summary>
/// <remarks>
/// On ne quitte jamais <em>uniquement</em> par Ctrl+C : le menu principal propose toujours une sortie. Ce
/// crochet existe pour qu'un Ctrl+C impatient laisse malgré tout la boîte à outils dans un état décrit au
/// lieu de la tuer en pleine écriture.
/// </remarks>
public sealed class CancellationHook : IDisposable
{
    private readonly CancellationTokenSource _source = new();
    private bool _hooked;
    private bool _disposed;

    /// <summary>Le jeton qui se déclenche quand le développeur appuie sur Ctrl+C.</summary>
    public CancellationToken Token => _source.Token;

    /// <summary>Indique si le développeur a demandé l'annulation.</summary>
    public bool IsCancellationRequested => _source.IsCancellationRequested;

    /// <summary>Installe le gestionnaire. Appelable une fois par processus.</summary>
    public void Install()
    {
        if (_hooked)
        {
            return;
        }

        // L'un des deux seuls usages autorisés de System.Console dans toute la solution. Tous les autres
        // sont interdits par la liste d'API bannies. Console.CancelKeyPress est le seul moyen d'intercepter
        // Ctrl+C, et Spectre.Console n'offre pas d'équivalent.
#pragma warning disable RS0030 // API bannie : interception autorisée de Ctrl+C, voir ci-dessus.
        global::System.Console.CancelKeyPress += OnCancelKeyPress;
#pragma warning restore RS0030

        _hooked = true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_hooked)
        {
#pragma warning disable RS0030 // API bannie : retrait du gestionnaire autorisé installé plus haut.
            global::System.Console.CancelKeyPress -= OnCancelKeyPress;
#pragma warning restore RS0030
            _hooked = false;
        }

        _source.Dispose();
        _disposed = true;
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
    {
        // Annuler le comportement par défaut d'arrêt immédiat pour que la fermeture soit ordonnée.
        eventArgs.Cancel = true;

        if (!_source.IsCancellationRequested)
        {
            _source.Cancel();
        }
    }
}
