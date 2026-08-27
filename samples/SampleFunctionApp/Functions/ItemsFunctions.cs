using AzureExtensions.Functions.OpenApi;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using SampleFunctionApp.Models;

namespace SampleFunctionApp.Functions;

/// <summary>
/// CRUD HTTP endpoints for <see cref="Item"/> that exercise every OpenAPI generator feature:
/// query, header, path parameters, request bodies, and typed responses.
/// </summary>
public sealed class ItemsFunctions
{
    private const string ItemsTag = "Items";

    private static readonly List<Item> Store = new()
    {
        new Item
        {
            Id = 1,
            Name = "Widget",
            Price = 9.99m,
            Status = ItemStatus.Active,
            Tags = new List<string> { "hardware", "featured" },
            Dimensions = new ItemDimensions { Width = 2.0, Height = 1.0, Depth = 0.5 },
            CreatedAt = DateTimeOffset.UtcNow,
        },
    };

    /// <summary>
    /// Lists items, optionally filtered by status and paged.
    /// </summary>
    [Function("ListItems")]
    [OpenApiOperation(
        OperationId = "listItems",
        Summary = "List items",
        Description = "Returns a paged list of catalog items, optionally filtered by status.",
        Tags = new[] { ItemsTag })]
    [OpenApiQueryParameter("status", typeof(ItemStatus), Required = false, Description = "Filter items by lifecycle status.")]
    [OpenApiQueryParameter("page", typeof(int), Required = false, Description = "1-based page number.")]
    [OpenApiResponse(200, Type = typeof(List<Item>), Description = "The matching items.")]
    [OpenApiResponseHeader("X-RateLimit-Remaining", typeof(int), 200, Description = "Requests remaining in the current window.")]
    public IResult ListItems(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "items")] HttpRequest req)
    {
        IEnumerable<Item> results = Store;

        if (Enum.TryParse<ItemStatus>(req.Query["status"], ignoreCase: true, out var status))
        {
            results = results.Where(i => i.Status == status);
        }

        return Results.Ok(results.ToList());
    }

    /// <summary>
    /// Gets a single item by identifier.
    /// </summary>
    [Function("GetItem")]
    [OpenApiOperation(
        OperationId = "getItem",
        Summary = "Get an item",
        Description = "Returns a single catalog item by its identifier.",
        Tags = new[] { ItemsTag })]
    [OpenApiPathParameter("id", typeof(int), Description = "The item identifier.")]
    [OpenApiResponse(200, Type = typeof(Item), Description = "The requested item.")]
    [OpenApiResponse(404, Type = typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), Description = "No item exists with the given identifier.")]
    public IResult GetItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "items/{id}")] HttpRequest req,
        int id)
    {
        var item = Store.FirstOrDefault(i => i.Id == id);
        return item is null
            ? Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Item not found",
                detail: $"No item exists with id {id}.")
            : Results.Ok(item);
    }

    /// <summary>
    /// Searches items by name, demonstrating a validation problem response.
    /// </summary>
    [Function("SearchItems")]
    [OpenApiOperation(
        OperationId = "searchItems",
        Summary = "Search items",
        Description = "Returns catalog items whose name contains the supplied search term.",
        Tags = new[] { ItemsTag })]
    [OpenApiQueryParameter("name", typeof(string), Required = true, Description = "The name (or partial name) to search for.")]
    [OpenApiResponse(200, Type = typeof(List<Item>), Description = "Matching items.")]
    [OpenApiResponse(400, Type = typeof(Microsoft.AspNetCore.Http.HttpValidationProblemDetails), Description = "The request was invalid.")]
    [OpenApiResponseHeader("x-continuation-token", typeof(string), 200, Description = "Opaque cursor for the next page.")]
    [OpenApiResponseHeader("X-Request-Id", typeof(Guid), 200, 400, Description = "Correlation id echoed on success and validation failure.")]
    public IResult SearchItems(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "items/search")] HttpRequest req)
    {
        var name = req.Query["name"].ToString();

        if (string.IsNullOrWhiteSpace(name))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = new[] { "The 'name' query parameter is required." },
            });
        }

        var results = Store
            .Where(i => i.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Results.Ok(results);
    }

    /// <summary>
    /// Creates a new item.
    /// </summary>
    [Function("CreateItem")]
    [OpenApiOperation(
        OperationId = "createItem",
        Summary = "Create an item",
        Description = "Creates a new catalog item and returns the created resource.",
        Tags = new[] { ItemsTag })]
    [OpenApiHeaderParameter("X-Correlation-Id", typeof(Guid), Required = false, Description = "Optional client correlation identifier.")]
    [OpenApiRequestBody(typeof(CreateItemRequest), Description = "The item to create.")]
    [OpenApiResponse(201, Type = typeof(Item), Description = "The created item.")]
    [OpenApiResponseHeader("Location", typeof(Uri), 201, Description = "URL of the newly created item.")]
    public async Task<IResult> CreateItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "items")] HttpRequest req)
    {
        var request = await req.ReadFromJsonAsync<CreateItemRequest>() ?? new CreateItemRequest();

        var item = new Item
        {
            Id = Store.Count == 0 ? 1 : Store.Max(i => i.Id) + 1,
            Name = request.Name,
            Price = request.Price,
            Status = request.Status,
            Tags = request.Tags,
            Dimensions = request.Dimensions,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        Store.Add(item);

        return Results.Created($"/api/items/{item.Id}", item);
    }

    /// <summary>
    /// Updates an existing item.
    /// </summary>
    [Function("UpdateItem")]
    [OpenApiOperation(
        OperationId = "updateItem",
        Summary = "Update an item",
        Description = "Updates an existing catalog item and returns the updated resource.",
        Tags = new[] { ItemsTag })]
    [OpenApiPathParameter("id", typeof(int), Description = "The item identifier.")]
    [OpenApiRequestBody(typeof(UpdateItemRequest), Description = "The updated item values.")]
    [OpenApiResponse(200, Type = typeof(Item), Description = "The updated item.")]
    [OpenApiResponse(404, Description = "No item exists with the given identifier.")]
    public async Task<IResult> UpdateItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "items/{id}")] HttpRequest req,
        int id)
    {
        var item = Store.FirstOrDefault(i => i.Id == id);
        if (item is null)
        {
            return Results.NotFound();
        }

        var request = await req.ReadFromJsonAsync<UpdateItemRequest>() ?? new UpdateItemRequest();

        item.Name = request.Name;
        item.Price = request.Price;
        item.Status = request.Status;
        item.Tags = request.Tags;
        item.Dimensions = request.Dimensions;

        return Results.Ok(item);
    }

    /// <summary>
    /// Deletes an item.
    /// </summary>
    [Function("DeleteItem")]
    [OpenApiOperation(
        OperationId = "deleteItem",
        Summary = "Delete an item",
        Description = "Deletes a catalog item by its identifier.",
        Tags = new[] { ItemsTag })]
    [OpenApiPathParameter("id", typeof(int), Description = "The item identifier.")]
    [OpenApiResponse(204, Description = "The item was deleted.")]
    [OpenApiResponse(404, Description = "No item exists with the given identifier.")]
    public IResult DeleteItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "items/{id}")] HttpRequest req,
        int id)
    {
        var item = Store.FirstOrDefault(i => i.Id == id);
        if (item is null)
        {
            return Results.NotFound();
        }

        Store.Remove(item);
        return Results.NoContent();
    }
}
