using Urman.Content.Validation;
using Xunit;

namespace Urman.Content.Tests;

public sealed class ContentWorkspaceValidatorTests
{
    [Fact]
    public async Task CurrentWorkspace_HasFourValidModulesAndCampaigns()
    {
        var root = FindWorkspaceRoot();
        var result = await new ContentWorkspaceValidator().ValidateAsync(root, TestContext.Current.CancellationToken);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(4, result.CheckedModules);
        Assert.Equal(4, result.CheckedCampaigns);
    }

    [Fact]
    public async Task MissingWorkspace_IsTypedDiagnostic()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"urman-missing-{Guid.NewGuid():N}");
        var result = await new ContentWorkspaceValidator().ValidateAsync(missing, TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("WorkspaceMissing", diagnostic.Code);
    }

    [Fact]
    public void CampaignSchema_RejectsMissingEntrypoint()
    {
        var root = FindWorkspaceRoot();
        var validator = ContentSchemaValidator.Load(Path.Combine(root, "content", "schemas"));
        var campaign = System.Text.Json.Nodes.JsonNode.Parse("""
            {
              "schemaVersion": 1,
              "id": "urman.test",
              "exactVersion": "1.0.0",
              "modules": [],
              "roleBindings": {},
              "capabilityRequirements": [],
              "narrativeOrder": [],
              "invariants": []
            }
            """)!;

        var diagnostics = validator.Validate("campaign-manifest.schema.json", campaign, "campaign.json");

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, diagnostic => Assert.Equal("InvalidSchema", diagnostic.Code));
    }

    private static string FindWorkspaceRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "content")) && File.Exists(Path.Combine(current.FullName, "Urman.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("URMAN workspace root was not found.");
    }
}
