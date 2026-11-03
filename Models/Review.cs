namespace OutfitStudio.Models;

public class Review
{
    public Guid UserKey
    {
        get; set;
    }
    [System.Text.Json.Serialization.JsonIgnore] public string Comment => Content;
    public int Id
    {
        get; set;
    }
    public int ProductId
    {
        get; set;
    }
    public int UserId
    {
        get; set;
    }
    public string UserName { get; set; } = "";
    public int Rating { get; set; } = 5;
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
