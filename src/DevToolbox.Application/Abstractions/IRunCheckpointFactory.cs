using DevToolbox.Domain.Runs;

namespace DevToolbox.Application.Abstractions;

/// <summary>
/// Construit le point de reprise d'une exécution. Implémentée par outil, car seul l'outil sait quelle part
/// de son état peut être conservée sans risque. Le moteur d'étapes est générique et ne doit jamais aller
/// fouiller lui-même dans le contexte d'un outil.
/// </summary>
public interface IRunCheckpointFactory
{
    /// <summary>L'outil dont cette fabrique sait conserver les exécutions.</summary>
    /// <remarks>
    /// Déclaré parce que le moteur d'étapes est partagé tandis que les fabriques sont propres à chaque
    /// outil : il faut donc pouvoir choisir la bonne. Sans cela, la dernière enregistrée l'emporterait, et
    /// comme une fabrique rend <see langword="null"/> pour un contexte qu'elle ne reconnaît pas, la reprise
    /// du second outil cesserait de fonctionner sans que rien ne le dise.
    /// </remarks>
    string ToolId { get; }

    /// <summary>Construit le point de reprise correspondant à l'état courant d'une exécution.</summary>
    /// <param name="context">Le contexte d'exécution.</param>
    /// <returns>
    /// Le point de reprise, ou <see langword="null"/> tant qu'il n'y a pas assez d'information pour en
    /// écrire un (par exemple avant que le projet ne soit identifié). Les implémentations ne doivent y
    /// inclure aucune valeur de variable.
    /// </returns>
    PersistedRun? TryCreate(ToolRunContext context);
}
