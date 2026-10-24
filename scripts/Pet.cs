namespace RambleRumble;

/// <summary>Each pet does one job at a time.</summary>
public enum PetRole
{
    None,
    Fence,
    /// <summary>Follows and fights beside the player; earns no fence gold.</summary>
    Battle,
}

/// <summary>A collected pet. Plain data, saved to JSON as-is.</summary>
public sealed class Pet
{
    public int Id { get; set; }
    public string Species { get; set; } = "";
    public string Rarity { get; set; } = "";
    public string Size { get; set; } = "Normal";
    public int Level { get; set; } = 1;
    public int Xp { get; set; }
    public int Stars { get; set; } = 1;
    public PetRole Role { get; set; } = PetRole.None;
}
