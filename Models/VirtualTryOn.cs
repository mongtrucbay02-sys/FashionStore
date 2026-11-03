namespace OutfitStudio.Models;

public sealed class VirtualTryOnRequest
{
    public string Source { get; set; } = "catalog";
    public string Scope { get; set; } = "outfit";
    public string Mode { get; set; } = "separates";
    public string? ShirtId
    {
        get; set;
    }
    public string? PantsId
    {
        get; set;
    }
    public string? SetId
    {
        get; set;
    }
    public string? Description
    {
        get; set;
    }
}

public sealed class VirtualTryOnPage
{
    public bool IsConfigured
    {
        get; set;
    }
    public bool IsAuthenticated
    {
        get; set;
    }
    public OutfitProduct[] Products { get; set; } = [];
}
