namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>Un chemin dérivé qui tombe sur un chemin que la production porte déjà.</summary>
/// <param name="Path">Le chemin en commun.</param>
/// <param name="Added">La route qui y est ajoutée.</param>
/// <param name="Subject">L'opération du contrat dont le chemin a été dérivé.</param>
/// <remarks>
/// Signalé, et non refusé. Ajouter une méthode sous un chemin que la production porte déjà est une
/// opération ordinaire et ne remplace rien ; la rendre impossible interdirait d'enrichir une ressource
/// existante. Ce qui est refusé ailleurs, et doit l'être, c'est d'écrire par-dessus une route que la
/// production porte déjà — méthode comprise.
/// <para>
/// Le signalement vaut parce que le chemin a été <em>dérivé</em> du nom d'une opération, et non déclaré.
/// Un chemin dérivé qui tombe sur une ressource existante est soit une bonne nouvelle — l'opération
/// appartient bien à cette ressource —, soit une collision de noms que personne n'a voulue, et seul le
/// développeur peut dire laquelle des deux.
/// </para>
/// </remarks>
public sealed record PathCollision(string Path, string Added, string Subject)
{
    /// <summary>Ce que la collision dit, en une ligne.</summary>
    public string Display =>
        Added + " — le chemin « " + Path + " » existe déjà en production, et il a été dérivé de « "
        + Subject + " ». Rien n'est remplacé : la méthode est ajoutée sous ce chemin.";
}
