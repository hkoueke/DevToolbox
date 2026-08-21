using DevToolbox.Domain.AzureDevOps;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.VarCompare.Presentation.Rendering;
using FluentAssertions;
using Spectre.Console.Testing;
using Xunit;

namespace DevToolbox.Tests.Presentation;

/// <summary>
/// Chaque échec que la passerelle peut produire est expliqué distinctement et sans rien laisser fuir.
/// </summary>
public sealed class FailureRendererTests
{
    private static readonly FailureReason[] AllReasons =
    [
        FailureReason.AuthenticationFailed,
        FailureReason.PermissionDenied,
        FailureReason.GroupNotFound,
        FailureReason.ServiceUnavailable,
        FailureReason.ServerUnreachable,
        FailureReason.ServerUntrusted,
        FailureReason.RedirectRefused,
        FailureReason.Cancelled,
    ];

    [Fact]
    public void Every_mapped_outcome_has_its_own_heading_and_advice()
    {
        List<string> headings = [];

        foreach (FailureReason reason in AllReasons)
        {
            (string heading, string advice) = FailureRenderer.Describe(reason);

            heading.Should().NotBeNullOrWhiteSpace();
            advice.Should().NotBeNullOrWhiteSpace();
            headings.Add(heading);
        }

        // Huit issues, huit explications distinctes, aucune ne retombant sur un message générique.
        headings.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void An_unreachable_server_and_a_permission_denial_are_not_confusable()
    {
        // Elles appellent des réactions entièrement différentes : vérifier le VPN, ou demander un accès.
        string unreachable = Render(FailureReason.ServerUnreachable, "could not reach the server");
        string denied = Render(FailureReason.PermissionDenied, "you cannot read this");

        Flatten(unreachable).Should().Contain("problème de connectivité, et non de droits");
        Flatten(denied).Should().Contain("accès en lecture");

        Flatten(unreachable).Should().NotContain("accès en lecture");
    }

    [Fact]
    public void An_authentication_failure_names_the_server_and_collection()
    {
        // Dire ce qui a été tenté, pour qu'une cible inattendue se remarque.
        string output = Render(
            FailureReason.AuthenticationFailed, "the server rejected your Windows credentials");

        output.Should().Contain("devops.entreprise.local");
        output.Should().Contain("DefaultCollection");
    }

    [Fact]
    public void An_authentication_failure_states_that_no_credential_will_be_asked_for()
    {
        // Un échec d'authentification n'est jamais une raison de se mettre à réclamer des identifiants.
        string output = Render(FailureReason.AuthenticationFailed, "rejected");

        Flatten(output).Should().Contain("ne demande jamais de mot de passe ni de jeton");
    }

    [Theory]
    [InlineData(FailureReason.AuthenticationFailed)]
    [InlineData(FailureReason.PermissionDenied)]
    [InlineData(FailureReason.ServiceUnavailable)]
    [InlineData(FailureReason.RedirectRefused)]
    public void No_message_offers_a_credential_prompt(FailureReason reason)
    {
        string output = Flatten(Render(reason, "something went wrong"));

        foreach (string forbidden in new[] { "saisissez votre mot de passe", "saisissez votre jeton" })
        {
            output.Should().NotContain(forbidden);
        }
    }

    [Fact]
    public void A_retry_exhaustion_tells_the_developer_the_tool_stopped_trying()
    {
        // Le signaler comme indisponible plutôt que de reprendre indéfiniment.
        Flatten(Render(FailureReason.ServiceUnavailable, "gave up"))
            .Should().Contain("cessé de réessayer");
    }

    [Fact]
    public void A_refused_redirect_explains_why_credentials_did_not_follow_it()
    {
        // Les identifiants ne suivent jamais une redirection.
        Flatten(Render(FailureReason.RedirectRefused, "redirect refused"))
            .Should().Contain("jamais présentés à un autre hôte");
    }

    [Fact]
    public void A_certificate_failure_says_validation_is_never_disabled()
    {
        // La validation des certificats n'est jamais désactivée.
        Flatten(Render(FailureReason.ServerUntrusted, "untrusted"))
            .Should().Contain("ne désactive jamais la validation des certificats");
    }

    [Fact]
    public void Cancelling_reassures_that_nothing_was_changed()
    {
        Flatten(Render(FailureReason.Cancelled, "cancelled"))
            .Should().Contain("ne fait jamais que lire");
    }

    private static string Flatten(string output) =>
        string.Join(" ", output.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Render(FailureReason reason, string message)
    {
        using TestConsole console = new();
        console.Profile.Width = 200;

        ServerTarget target = new(
            new Uri("https://devops.entreprise.local"), "DefaultCollection", "7.1");

        new FailureRenderer(console, target).Render(Failure.Of(reason, message));

        return console.Output;
    }
}
