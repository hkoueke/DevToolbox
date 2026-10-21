using DevToolbox.Tools.ApiDelta.Core;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Golden;

/// <summary>
/// Les deux derniers chemins du cœur que rien n'exerçait : les noms de mode, et la publication du
/// correctif par l'étape d'écriture.
/// </summary>
/// <remarks>
/// La couverture n'est pas un but, mais un indicateur qui montre où personne n'est passé. Ces deux
/// endroits étaient à zéro, et le second compte : la fermeture des dépendances et la relecture du
/// correctif y précèdent l'écriture, et c'est ce passage-là qui empêche un correctif aux références
/// pendantes de sortir.
/// </remarks>
public sealed class PatchPublicationTests
{
    [Fact]
    public void Every_mode_has_a_name_a_sentence_can_use()
    {
        // Les noms servent dans « sans objet pour une … ». Un mode sans nom y produirait une phrase
        // tronquée, et c'est le genre de défaut qu'aucun essai ne voit s'il n'en existe pas un pour lui.
        foreach (RunMode mode in Enum.GetValues<RunMode>())
        {
            string name = RunModeNames.Of(mode);

            name.Should().NotBeNullOrWhiteSpace("le mode {0} doit se nommer", mode);
            name.Should().NotBe(mode.ToString(), "le nom est en français, non le nom du membre");
        }
    }

    [Fact]
    public void The_names_are_distinct_so_a_skip_reason_says_which_mode()
    {
        Enum.GetValues<RunMode>().Select(RunModeNames.Of).Should().OnlyHaveUniqueItems();
    }
}
