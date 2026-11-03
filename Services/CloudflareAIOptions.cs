namespace OutfitStudio.Services;

public sealed class CloudflareAIOptions
{
    public string ApiKey { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string Model { get; set; } = "@cf/black-forest-labs/flux-2-dev";
}
