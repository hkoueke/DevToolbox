namespace DevToolbox.Console;

/// <summary>Ce que la coquille rapporte en rendant la main.</summary>
/// <param name="ExitCode">Le code de sortie, quand l'application s'arrête.</param>
/// <param name="ToolToRunAfterRestart">
/// L'outil à lancer après reconstruction de l'hôte, ou <see langword="null"/> quand il n'y a rien à
/// reprendre.
/// </param>
/// <remarks>
/// Le second membre n'existe que pour un cas : un outil adressé au serveur a été choisi alors qu'aucune
/// adresse n'était enregistrée. L'adresse vient d'être confirmée, mais les options d'un hôte construit sont
/// déjà liées et les outils déjà instanciés portent la cible qu'ils ont reçue. Reconstruire est la seule
/// manière honnête de leur donner la bonne, et ce membre est ce qui permet de reprendre là où le
/// développeur en était plutôt que de le renvoyer au menu.
/// </remarks>
internal sealed record ShellResult(int ExitCode, string? ToolToRunAfterRestart);
