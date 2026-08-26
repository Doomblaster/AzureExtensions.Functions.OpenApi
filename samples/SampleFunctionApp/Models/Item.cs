namespace SampleFunctionApp.Models;

/// <summary>
/// Lifecycle status of an <see cref="Item"/>.
/// </summary>
public enum ItemStatus
{
    Active,
    Discontinued,
    Backordered,
}

/// <summary>
/// Physical dimensions of an item, used to exercise nested-class schema generation.
/// </summary>
public sealed class ItemDimensions
{
    public double Width { get; set; }

    public double Height { get; set; }

    public double Depth { get; set; }
}

/// <summary>
/// A catalog item that exercises enum, collection, decimal, nested class, and nullable schema features.
/// </summary>
public sealed class Item
{
    public int Id { get; set; }

    public Guid PublicId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public ItemStatus Status { get; set; }

    public List<string> Tags { get; set; } = new();

    public ItemDimensions? Dimensions { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateOnly ReleaseDate { get; set; }

    public TimeOnly RestockTime { get; set; }

    public DateTime? DiscontinuedAt { get; set; }
}

/// <summary>
/// Request payload for creating a new <see cref="Item"/>.
/// </summary>
public sealed class CreateItemRequest
{
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public ItemStatus Status { get; set; }

    public List<string> Tags { get; set; } = new();

    public ItemDimensions? Dimensions { get; set; }
}

/// <summary>
/// Request payload for updating an existing <see cref="Item"/>.
/// </summary>
public sealed class UpdateItemRequest
{
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public ItemStatus Status { get; set; }

    public List<string> Tags { get; set; } = new();

    public ItemDimensions? Dimensions { get; set; }
}
