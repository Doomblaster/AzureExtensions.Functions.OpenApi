using AzureExtensions.Functions.OpenApi;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;

namespace SampleFunctionApp.Models
{
    public enum ItemStatus
    {
        Active,
        Discontinued,
        Backordered,
    }

    public sealed class ItemDimensions
    {
        public decimal Width { get; set; }

        public decimal Height { get; set; }
    }

    public sealed class Item
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public ItemStatus Status { get; set; }

        public Guid PublicId { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateOnly ReleaseDate { get; set; }

        public TimeOnly RestockTime { get; set; }

        public DateTimeOffset? DiscontinuedAt { get; set; }

        public ItemDimensions? Dimensions { get; set; }
    }

    public sealed class CreateItemRequest
    {
        public string Name { get; set; } = string.Empty;

        public ItemStatus Status { get; set; }

        public ItemDimensions? Dimensions { get; set; }
    }

    public sealed class UpdateItemRequest
    {
        public string Name { get; set; } = string.Empty;

        public ItemStatus Status { get; set; }

        public ItemDimensions? Dimensions { get; set; }
    }
}

namespace SampleFunctionApp.Functions
{
    using SampleFunctionApp.Models;

    public sealed class ItemsFunctions
    {
        [Function("ListItems")]
        [OpenApiOperation(OperationId = "listItems", Summary = "List items")]
        [OpenApiQueryParameter("status", typeof(ItemStatus), Required = false, Description = "Optional item status filter.")]
        [OpenApiResponse(200, Type = typeof(List<Item>), Description = "Items.")]
        [OpenApiResponseHeader("X-RateLimit-Remaining", typeof(int), 200, Description = "Requests remaining.")]
        public IResult ListItems(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "items")] HttpRequest req)
            => Results.Ok(Array.Empty<Item>());

        [Function("GetItem")]
        [OpenApiOperation(OperationId = "getItem", Summary = "Get item")]
        [OpenApiPathParameter("id", typeof(int), Description = "Item identifier.")]
        [OpenApiResponse(200, Type = typeof(Item), Description = "Found.")]
        [OpenApiResponse(404, Type = typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), Description = "Missing.")]
        public IResult GetItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "items/{id}")] HttpRequest req)
            => Results.Ok(new Item());

        [Function("CreateItem")]
        [OpenApiOperation(OperationId = "createItem", Summary = "Create item")]
        [OpenApiRequestBody(typeof(CreateItemRequest), Description = "Item to create.")]
        [OpenApiResponse(201, Type = typeof(Item), Description = "Created.")]
        [OpenApiResponseHeader("Location", typeof(Uri), 201, Description = "URL of the newly created item.")]
        public IResult CreateItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "items")] HttpRequest req)
            => Results.Created("https://example.test/api/items/1", new Item());

        [Function("UpdateItem")]
        [OpenApiOperation(OperationId = "updateItem", Summary = "Update item")]
        [OpenApiPathParameter("id", typeof(int), Description = "Item identifier.")]
        [OpenApiRequestBody(typeof(UpdateItemRequest), Description = "Item update.")]
        [OpenApiResponse(200, Type = typeof(Item), Description = "Updated.")]
        public IResult UpdateItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "items/{id}")] HttpRequest req)
            => Results.Ok(new Item());

        [Function("DeleteItem")]
        [OpenApiOperation(OperationId = "deleteItem", Summary = "Delete item")]
        [OpenApiPathParameter("id", typeof(int), Description = "Item identifier.")]
        [OpenApiResponse(204, Description = "Deleted.")]
        public IResult DeleteItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "items/{id}")] HttpRequest req)
            => Results.NoContent();

        [Function("SearchItems")]
        [OpenApiOperation(OperationId = "searchItems", Summary = "Search items")]
        [OpenApiQueryParameter("name", typeof(string), Required = true, Description = "Name fragment.")]
        [OpenApiResponse(200, Type = typeof(List<Item>), Description = "Matches.")]
        [OpenApiResponse(400, Type = typeof(Microsoft.AspNetCore.Http.HttpValidationProblemDetails), Description = "Validation error.")]
        public IResult SearchItems(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "items/search")] HttpRequest req)
            => Results.Ok(Array.Empty<Item>());
    }
}
