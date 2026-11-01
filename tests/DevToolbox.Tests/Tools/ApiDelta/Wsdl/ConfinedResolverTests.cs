using System.Text;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

public sealed class ConfinedResolverTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Replacing_or_deleting_a_validated_file_does_not_change_the_resolved_content(bool delete)
    {
        string directory = Path.Combine(Path.GetTempPath(), "resolver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "schema.xsd");
        try
        {
            File.WriteAllText(path, "validated");
            Uri uri = new(path);
            ConfinedResolver resolver = new(
                new Dictionary<string, string>(StringComparer.Ordinal) { [uri.ToString()] = path },
                new Dictionary<string, byte[]>(StringComparer.Ordinal) { [path] = File.ReadAllBytes(path) });
            if (delete)
            {
                File.Delete(path);
            }
            else
            {
                File.WriteAllText(path, new string('X', 100_000));
            }

            using Stream resolved = (Stream)resolver.GetEntity(uri, null, null)!;
            using StreamReader reader = new(resolved, Encoding.UTF8);

            reader.ReadToEnd().Should().Be("validated");
            resolved.CanWrite.Should().BeFalse();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Both_an_allowed_URI_and_validated_content_are_required(bool allowed, bool validated)
    {
        string path = Path.Combine(Path.GetTempPath(), "not-opened-" + Guid.NewGuid().ToString("N") + ".xsd");
        Uri uri = new(path);
        Dictionary<string, string> locations = new(StringComparer.Ordinal);
        Dictionary<string, byte[]> contents = new(StringComparer.Ordinal);
        if (allowed)
        {
            locations.Add(uri.ToString(), path);
        }

        if (validated)
        {
            contents.Add(path, Encoding.UTF8.GetBytes("validated"));
        }

        ConfinedResolver resolver = new(locations, contents);

        resolver.GetEntity(uri, null, null).Should().BeNull();
    }
}
