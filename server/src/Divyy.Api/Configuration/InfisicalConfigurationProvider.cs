using Infisical.Sdk;
using Infisical.Sdk.Model;

namespace Divvy.Api.Configuration;

/// <summary>
/// IConfiguration source that loads secrets from Infisical using Universal Auth (Machine Identity).
/// Secrets named with double-underscore notation (e.g. Jwt__Key) are mapped to the ASP.NET Core
/// colon hierarchy (Jwt:Key) automatically.
/// </summary>
public class InfisicalConfigurationSource : IConfigurationSource
{
    private readonly IConfiguration _bootstrapConfig;
    private readonly string _environmentSlug;

    public InfisicalConfigurationSource(IConfiguration bootstrapConfig, string environmentSlug)
    {
        _bootstrapConfig = bootstrapConfig;
        _environmentSlug = environmentSlug;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new InfisicalConfigurationProvider(_bootstrapConfig, _environmentSlug);
}

public class InfisicalConfigurationProvider : ConfigurationProvider
{
    private readonly IConfiguration _bootstrapConfig;
    private readonly string _environmentSlug;

    public InfisicalConfigurationProvider(IConfiguration bootstrapConfig, string environmentSlug)
    {
        _bootstrapConfig = bootstrapConfig;
        _environmentSlug = environmentSlug;
    }

    public override void Load()
    {
        var clientId     = _bootstrapConfig["Infisical:ClientId"];
        var clientSecret = _bootstrapConfig["Infisical:ClientSecret"];
        var projectId    = _bootstrapConfig["Infisical:ProjectId"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(projectId))
        {
            Console.WriteLine("[Infisical] Skipping — ClientId, ClientSecret, or ProjectId not configured.");
            return;
        }

        try
        {
            LoadAsync(clientId, clientSecret, projectId).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"[Infisical] Failed to load secrets for environment '{_environmentSlug}': {ex.Message}", ex);
        }
    }

    private async Task LoadAsync(string clientId, string clientSecret, string projectId)
    {
        var settings  = new InfisicalSdkSettingsBuilder().Build();
        var infisical = new InfisicalClient(settings);

        await infisical.Auth().UniversalAuth().LoginAsync(clientId, clientSecret);

        var secrets = await infisical.Secrets().ListAsync(new ListSecretsOptions
        {
            ProjectId                    = projectId,
            EnvironmentSlug              = _environmentSlug,
            SecretPath                   = "/",
            SetSecretsAsEnvironmentVariables = false,
        });

        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var secret in secrets)
        {
            // Map Infisical double-underscore convention → ASP.NET Core colon hierarchy
            var key = secret.SecretKey.Replace("__", ":");
            // Strip ALL control characters (null bytes, newlines, carriage returns etc.)
            // that Infisical's SDK may inject, not just leading/trailing whitespace.
            var raw   = secret.SecretValue ?? string.Empty;
            var clean = new string(raw.Where(c => !char.IsControl(c)).ToArray()).Trim();
            data[key] = clean;
            Console.WriteLine($"[Infisical] Secret '{key}' length: raw={raw.Length} clean={clean.Length}");
        }

        Data = data;
        Console.WriteLine($"[Infisical] Loaded {data.Count} secrets from environment '{_environmentSlug}'.");
    }
}

public static class InfisicalConfigurationExtensions
{
    /// <summary>
    /// Adds Infisical as an IConfiguration source. Reads Infisical:ClientId, Infisical:ClientSecret,
    /// and Infisical:ProjectId from <paramref name="bootstrapConfig"/>.
    /// </summary>
    public static IConfigurationBuilder AddInfisical(
        this IConfigurationBuilder builder,
        IConfiguration bootstrapConfig,
        string environmentSlug)
    {
        return builder.Add(new InfisicalConfigurationSource(bootstrapConfig, environmentSlug));
    }
}
