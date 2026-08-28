namespace AzureExtensions.Functions.OpenApi.Tests.SchemaCollision.Alpha
{
    internal sealed class Item
    {
        public string FirstName { get; set; } = string.Empty;

        public int Quantity { get; set; }
    }
}

namespace AzureExtensions.Functions.OpenApi.Tests.SchemaCollision.A
{
    internal sealed class Item
    {
        public bool Enabled { get; set; }

        public Guid ExternalId { get; set; }
    }
}

namespace AzureExtensions.Functions.OpenApi.Tests.Other.A
{
    internal sealed class Item
    {
        public decimal Price { get; set; }

        public DateOnly EffectiveDate { get; set; }
    }
}
