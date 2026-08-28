using System.Net.Http;
using System.Reflection;
using AzureExtensions.Functions.OpenApi.Discovery;
using AzureExtensions.Functions.OpenApi.Schema;
using Microsoft.OpenApi;

namespace AzureExtensions.Functions.OpenApi.Generation;

/// <summary>
/// Turns discovered HTTP-triggered function endpoints and their OpenAPI attributes into the
/// <see cref="OpenApiDocument.Paths"/> and <see cref="OpenApiDocument.Components"/> of a document.
/// </summary>
/// <remarks>
/// <para>
/// This builder is the bridge between reflection-only discovery
/// (<see cref="FunctionEndpointDiscovery"/>) and the Microsoft.OpenApi (3.10.2) object model. It
/// does not perform discovery itself: the caller passes the already-discovered endpoints. Schema
/// construction is delegated to a single <see cref="OpenApiSchemaGenerator"/> instance so that
/// complex types are registered once into the shared <see cref="OpenApiComponents.Schemas"/> and
/// referenced by <c>$ref</c> everywhere they are used.
/// </para>
/// <para>
/// Request headers can come from individual <see cref="OpenApiRequestHeaderParameterAttribute"/>
/// instances or reusable <see cref="OpenApiRequestHeaderParameterSetAttribute"/> collections. Response headers can
/// come from individual <see cref="OpenApiResponseHeaderAttribute"/> instances or reusable
/// <see cref="OpenApiResponseHeaderSetAttribute"/> collections. In every case, header schemas are
/// produced through the shared <see cref="OpenApiSchemaGenerator"/>.
/// </para>
/// <para>
/// The build is resilient: an exception while building a single endpoint is swallowed and that
/// endpoint is skipped, so one malformed method can never abort the whole document.
/// </para>
/// <para>
/// 3.10.2 model notes: <see cref="OpenApiPathItem.Operations"/> is keyed by
/// <see cref="System.Net.Http.HttpMethod"/> (not a bespoke verb enum);
/// <see cref="OpenApiOperation.Tags"/> is an <see cref="ISet{T}"/> of
/// <see cref="OpenApiTagReference"/>; parameters are <see cref="IOpenApiParameter"/>; request-body
/// and response content are dictionaries keyed by media type whose values are
/// <see cref="IOpenApiMediaType"/>; and <see cref="OpenApiResponses"/> is keyed by the status-code
/// string.
/// </para>
/// </remarks>
internal sealed class OpenApiPathsBuilder
{
    private readonly OpenApiSchemaGenerator _schemaGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiPathsBuilder"/> class with a fresh
    /// <see cref="OpenApiSchemaGenerator"/>.
    /// </summary>
    public OpenApiPathsBuilder()
        : this(new OpenApiSchemaGenerator())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiPathsBuilder"/> class.
    /// </summary>
    /// <param name="schemaGenerator">
    /// The schema generator used to map CLR types to OpenAPI schemas. A single instance should be
    /// used for the whole document build so complex-type registration and de-duplication work.
    /// </param>
    public OpenApiPathsBuilder(OpenApiSchemaGenerator schemaGenerator)
    {
        ArgumentNullException.ThrowIfNull(schemaGenerator);
        _schemaGenerator = schemaGenerator;
    }

    /// <summary>
    /// Populates <paramref name="document"/> with paths and components derived from
    /// <paramref name="endpoints"/>.
    /// </summary>
    /// <param name="document">The document to populate. <see cref="OpenApiDocument.Paths"/> and
    /// <see cref="OpenApiDocument.Components"/> are created when <see langword="null"/>.</param>
    /// <param name="endpoints">The discovered endpoints to document.</param>
    /// <param name="includeUnannotated">
    /// When <see langword="true"/>, endpoints carrying no OpenAPI attributes are still documented
    /// via best-effort inference (inferred path parameters plus a default <c>200</c> response).
    /// When <see langword="false"/>, such endpoints are omitted.
    /// </param>
    public void Populate(
        OpenApiDocument document,
        IReadOnlyList<DiscoveredEndpoint> endpoints,
        bool includeUnannotated)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(endpoints);

        document.Paths ??= new OpenApiPaths();
        var components = document.Components ??= new OpenApiComponents();

        foreach (var endpoint in endpoints)
        {
            try
            {
                AddEndpoint(document, components, endpoint, includeUnannotated);
            }
            catch
            {
                // A single malformed endpoint must never abort the whole document build.
            }
        }
    }

    private void AddEndpoint(
        OpenApiDocument document,
        OpenApiComponents components,
        DiscoveredEndpoint endpoint,
        bool includeUnannotated)
    {
        var method = endpoint.Method;

        var operationAttribute = method.GetCustomAttribute<OpenApiOperationAttribute>();
        var pathParamAttributes = method.GetCustomAttributes<OpenApiPathParameterAttribute>().ToList();
        var queryParamAttributes = method.GetCustomAttributes<OpenApiQueryParameterAttribute>().ToList();
        var headerParamAttributes = method.GetCustomAttributes<OpenApiRequestHeaderParameterAttribute>().ToList();
        var headerParamSetAttributes = method.GetCustomAttributes<OpenApiRequestHeaderParameterSetAttribute>().ToList();
        var requestBodyAttribute = method.GetCustomAttribute<OpenApiRequestBodyAttribute>();
        var responseAttributes = method.GetCustomAttributes<OpenApiResponseAttribute>().ToList();
        var responseHeaderAttributes = method.GetCustomAttributes<OpenApiResponseHeaderAttribute>().ToList();
        var responseHeaderSetAttributes = method.GetCustomAttributes<OpenApiResponseHeaderSetAttribute>().ToList();

        var hasAnyAttribute =
            operationAttribute is not null ||
            pathParamAttributes.Count > 0 ||
            queryParamAttributes.Count > 0 ||
            headerParamAttributes.Count > 0 ||
            headerParamSetAttributes.Count > 0 ||
            requestBodyAttribute is not null ||
            responseAttributes.Count > 0 ||
            responseHeaderAttributes.Count > 0 ||
            responseHeaderSetAttributes.Count > 0;

        if (!hasAnyAttribute && !includeUnannotated)
        {
            return;
        }

        var methods = endpoint.HttpMethods.Count > 0
            ? (IReadOnlyList<string>)endpoint.HttpMethods
            : ["GET"];

        var operations = new List<KeyValuePair<HttpMethod, OpenApiOperation>>(methods.Count);

        foreach (var httpMethod in methods)
        {
            var operation = BuildOperation(
                components,
                operationAttribute,
                endpoint.RouteParameters,
                pathParamAttributes,
                queryParamAttributes,
                headerParamSetAttributes,
                headerParamAttributes,
                requestBodyAttribute,
                responseAttributes,
                responseHeaderAttributes,
                responseHeaderSetAttributes);

            operations.Add(new KeyValuePair<HttpMethod, OpenApiOperation>(ParseHttpMethod(httpMethod), operation));
        }

        var pathItem = GetOrCreatePathItem(document, endpoint.Path);

        foreach (var operation in operations)
        {
            pathItem.Operations![operation.Key] = operation.Value;
        }
    }

    private OpenApiOperation BuildOperation(
        OpenApiComponents components,
        OpenApiOperationAttribute? operationAttribute,
        IReadOnlyList<string> routeParameters,
        IReadOnlyList<OpenApiPathParameterAttribute> pathParamAttributes,
        IReadOnlyList<OpenApiQueryParameterAttribute> queryParamAttributes,
        IReadOnlyList<OpenApiRequestHeaderParameterSetAttribute> headerParamSetAttributes,
        IReadOnlyList<OpenApiRequestHeaderParameterAttribute> headerParamAttributes,
        OpenApiRequestBodyAttribute? requestBodyAttribute,
        IReadOnlyList<OpenApiResponseAttribute> responseAttributes,
        IReadOnlyList<OpenApiResponseHeaderAttribute> responseHeaderAttributes,
        IReadOnlyList<OpenApiResponseHeaderSetAttribute> responseHeaderSetAttributes)
    {
        var operation = new OpenApiOperation
        {
            Parameters = new List<IOpenApiParameter>(),
        };

        if (operationAttribute is not null)
        {
            operation.OperationId = operationAttribute.OperationId;
            operation.Summary = operationAttribute.Summary;
            operation.Description = operationAttribute.Description;
            operation.Deprecated = operationAttribute.Deprecated;

            if (operationAttribute.Tags is { Length: > 0 } tags)
            {
                operation.Tags = new HashSet<OpenApiTagReference>();
                foreach (var tag in tags)
                {
                    if (!string.IsNullOrWhiteSpace(tag))
                    {
                        operation.Tags.Add(new OpenApiTagReference(tag));
                    }
                }
            }
        }

        AddPathParameters(operation, components, routeParameters, pathParamAttributes);
        AddQueryParameters(operation, components, queryParamAttributes);
        AddHeaderParameters(operation, components, headerParamSetAttributes, headerParamAttributes);

        if (requestBodyAttribute is not null)
        {
            operation.RequestBody = BuildRequestBody(components, requestBodyAttribute);
        }

        operation.Responses = BuildResponses(
            components,
            responseAttributes,
            responseHeaderSetAttributes,
            responseHeaderAttributes);

        return operation;
    }

    private void AddPathParameters(
        OpenApiOperation operation,
        OpenApiComponents components,
        IReadOnlyList<string> routeParameters,
        IReadOnlyList<OpenApiPathParameterAttribute> pathParamAttributes)
    {
        // Route-token names first (default string schema), preserving discovery order.
        var order = new List<string>();
        var byName = new Dictionary<string, OpenApiParameter>(StringComparer.Ordinal);

        foreach (var name in routeParameters)
        {
            if (byName.ContainsKey(name))
            {
                continue;
            }

            byName[name] = new OpenApiParameter
            {
                Name = name,
                In = ParameterLocation.Path,
                Required = true,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            };
            order.Add(name);
        }

        // Attribute-declared path parameters enrich or override the inferred ones.
        foreach (var attribute in pathParamAttributes)
        {
            if (!byName.TryGetValue(attribute.Name, out var parameter))
            {
                parameter = new OpenApiParameter { Name = attribute.Name };
                byName[attribute.Name] = parameter;
                order.Add(attribute.Name);
            }

            parameter.In = ParameterLocation.Path;
            parameter.Required = attribute.Required;
            parameter.Description = attribute.Description;
            parameter.Schema = _schemaGenerator.GetOrCreateSchema(attribute.Type, components);
        }

        foreach (var name in order)
        {
            operation.Parameters!.Add(byName[name]);
        }
    }

    private void AddQueryParameters(
        OpenApiOperation operation,
        OpenApiComponents components,
        IReadOnlyList<OpenApiQueryParameterAttribute> queryParamAttributes)
    {
        foreach (var attribute in queryParamAttributes)
        {
            operation.Parameters!.Add(new OpenApiParameter
            {
                Name = attribute.Name,
                In = ParameterLocation.Query,
                Required = attribute.Required,
                Description = attribute.Description,
                Schema = _schemaGenerator.GetOrCreateSchema(attribute.Type, components),
            });
        }
    }

    private void AddHeaderParameters(
        OpenApiOperation operation,
        OpenApiComponents components,
        IReadOnlyList<OpenApiRequestHeaderParameterSetAttribute> headerParamSetAttributes,
        IReadOnlyList<OpenApiRequestHeaderParameterAttribute> headerParamAttributes)
    {
        var individualHeaderNames = new HashSet<string>(
            headerParamAttributes.Select(static attribute => attribute.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var setAttribute in headerParamSetAttributes)
        {
            var collection = CreateHeaderDefinitionCollection<IOpenApiHeaderDefinitionCollection>(
                setAttribute.CollectionType,
                nameof(OpenApiRequestHeaderParameterSetAttribute));
            var headers = collection.Headers ?? throw new InvalidOperationException(
                $"CollectionType '{setAttribute.CollectionType.FullName}' for {nameof(OpenApiRequestHeaderParameterSetAttribute)} returned null {nameof(IOpenApiHeaderDefinitionCollection.Headers)}.");

            foreach (var header in headers)
            {
                if (individualHeaderNames.Contains(header.Name))
                {
                    continue;
                }

                operation.Parameters!.Add(
                    CreateHeaderParameter(components, header.Name, header.Type, header.Required, header.Description, header.Deprecated));
            }
        }

        foreach (var attribute in headerParamAttributes)
        {
            operation.Parameters!.Add(
                CreateHeaderParameter(components, attribute.Name, attribute.Type, attribute.Required, attribute.Description, attribute.Deprecated));
        }
    }

    private OpenApiRequestBody BuildRequestBody(
        OpenApiComponents components,
        OpenApiRequestBodyAttribute attribute)
    {
        return new OpenApiRequestBody
        {
            Required = attribute.Required,
            Description = attribute.Description,
            Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
            {
                [ResolveContentType(attribute.Type, attribute.ContentType)] = new OpenApiMediaType
                {
                    Schema = _schemaGenerator.GetOrCreateSchema(attribute.Type, components),
                },
            },
        };
    }

    private OpenApiResponses BuildResponses(
        OpenApiComponents components,
        IReadOnlyList<OpenApiResponseAttribute> responseAttributes,
        IReadOnlyList<OpenApiResponseHeaderSetAttribute> responseHeaderSetAttributes,
        IReadOnlyList<OpenApiResponseHeaderAttribute> responseHeaderAttributes)
    {
        var responses = new OpenApiResponses();

        if (responseAttributes.Count == 0)
        {
            responses["200"] = new OpenApiResponse { Description = "Success" };
            ApplyResponseHeaders(components, responses, responseHeaderSetAttributes, responseHeaderAttributes);
            return responses;
        }

        foreach (var attribute in responseAttributes)
        {
            var response = new OpenApiResponse
            {
                Description = attribute.Description ?? string.Empty,
            };

            if (attribute.Type is not null)
            {
                response.Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
                {
                    [ResolveContentType(attribute.Type, attribute.ContentType)] = new OpenApiMediaType
                    {
                        Schema = _schemaGenerator.GetOrCreateSchema(attribute.Type, components),
                    },
                };
            }

            responses[attribute.StatusCode.ToString()] = response;
        }

        ApplyResponseHeaders(components, responses, responseHeaderSetAttributes, responseHeaderAttributes);

        return responses;
    }

    private void ApplyResponseHeaders(
        OpenApiComponents components,
        OpenApiResponses responses,
        IReadOnlyList<OpenApiResponseHeaderSetAttribute> responseHeaderSetAttributes,
        IReadOnlyList<OpenApiResponseHeaderAttribute> responseHeaderAttributes)
    {
        foreach (var setAttribute in responseHeaderSetAttributes)
        {
            var collection = CreateHeaderDefinitionCollection<IOpenApiHeaderDefinitionCollection>(
                setAttribute.CollectionType,
                nameof(OpenApiResponseHeaderSetAttribute));
            var headers = collection.Headers ?? throw new InvalidOperationException(
                $"CollectionType '{setAttribute.CollectionType.FullName}' for {nameof(OpenApiResponseHeaderSetAttribute)} returned null {nameof(IOpenApiHeaderDefinitionCollection.Headers)}.");
            var targetKeys = GetTargetResponseKeys(responses, setAttribute.StatusCodes);

            foreach (var header in headers)
            {
                ApplyResponseHeaderSetMember(
                    components,
                    responses,
                    targetKeys,
                    responseHeaderAttributes,
                    header.Name,
                    header.Type,
                    header.Description,
                    header.Required,
                    header.Deprecated);
            }
        }

        foreach (var attribute in responseHeaderAttributes)
        {
            ApplyResponseHeader(
                components,
                responses,
                GetTargetResponseKeys(responses, attribute.StatusCodes),
                attribute.Name,
                attribute.Type,
                attribute.Description,
                attribute.Required,
                attribute.Deprecated);
        }
    }

    private static TCollection CreateHeaderDefinitionCollection<TCollection>(
        Type collectionType,
        string attributeName)
        where TCollection : class
    {
        ArgumentNullException.ThrowIfNull(collectionType);

        if (!typeof(TCollection).IsAssignableFrom(collectionType))
        {
            throw new InvalidOperationException(
                $"{attributeName} requires CollectionType '{collectionType.FullName}' to implement {typeof(TCollection).Name}.");
        }

        if (collectionType.IsAbstract || collectionType.IsInterface)
        {
            throw new InvalidOperationException(
                $"{attributeName} requires CollectionType '{collectionType.FullName}' to be a concrete, non-abstract type.");
        }

        if (collectionType.GetConstructor(Type.EmptyTypes) is null)
        {
            throw new InvalidOperationException(
                $"{attributeName} requires CollectionType '{collectionType.FullName}' to have a public parameterless constructor.");
        }

        try
        {
            return (TCollection)Activator.CreateInstance(collectionType)!;
        }
        catch (Exception ex) when (ex is MemberAccessException or TargetInvocationException)
        {
            throw new InvalidOperationException(
                $"Failed to instantiate CollectionType '{collectionType.FullName}' for {attributeName}.",
                ex);
        }
    }

    private OpenApiParameter CreateHeaderParameter(
        OpenApiComponents components,
        string name,
        Type type,
        bool required,
        string? description,
        bool deprecated)
    {
        return new OpenApiParameter
        {
            Name = name,
            In = ParameterLocation.Header,
            Required = required,
            Description = description,
            Deprecated = deprecated,
            Schema = _schemaGenerator.GetOrCreateSchema(type, components),
        };
    }

    private static IEnumerable<string> GetTargetResponseKeys(OpenApiResponses responses, int[] statusCodes)
    {
        if (statusCodes.Length == 0)
        {
            // An empty list targets only already-present responses.
            return responses.Keys.ToList();
        }

        foreach (var statusCode in statusCodes)
        {
            var key = statusCode.ToString();
            if (!responses.ContainsKey(key))
            {
                responses[key] = new OpenApiResponse { Description = string.Empty };
            }
        }

        return statusCodes.Select(static c => c.ToString()).ToArray();
    }

    private void ApplyResponseHeaderSetMember(
        OpenApiComponents components,
        OpenApiResponses responses,
        IEnumerable<string> targetKeys,
        IReadOnlyList<OpenApiResponseHeaderAttribute> responseHeaderAttributes,
        string name,
        Type type,
        string? description,
        bool required,
        bool deprecated)
    {
        foreach (var key in targetKeys)
        {
            if (HasMatchingIndividualResponseHeader(responseHeaderAttributes, responses, key, name))
            {
                continue;
            }

            ApplyResponseHeader(
                components,
                responses,
                [key],
                name,
                type,
                description,
                required,
                deprecated);
        }
    }

    private static bool HasMatchingIndividualResponseHeader(
        IReadOnlyList<OpenApiResponseHeaderAttribute> responseHeaderAttributes,
        OpenApiResponses responses,
        string statusKey,
        string name)
    {
        foreach (var attribute in responseHeaderAttributes)
        {
            if (!string.Equals(attribute.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (attribute.StatusCodes.Length == 0)
            {
                if (responses.ContainsKey(statusKey))
                {
                    return true;
                }

                continue;
            }

            if (attribute.StatusCodes.Any(statusCode => string.Equals(statusCode.ToString(), statusKey, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyResponseHeader(
        OpenApiComponents components,
        OpenApiResponses responses,
        IEnumerable<string> targetKeys,
        string name,
        Type type,
        string? description,
        bool required,
        bool deprecated)
    {
        foreach (var key in targetKeys)
        {
            if (responses[key] is not OpenApiResponse response)
            {
                continue;
            }

            response.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal);
            response.Headers[name] = new OpenApiHeader
            {
                Schema = _schemaGenerator.GetOrCreateSchema(type, components),
                Description = description,
                Required = required,
                Deprecated = deprecated,
            };
        }
    }

    // The RFC 9457 media type for problem responses. The ProblemDetails family is served as
    // application/problem+json unless the attribute explicitly declared a non-default media type.
    private const string ProblemJsonContentType = "application/problem+json";
    private const string DefaultJsonContentType = "application/json";

    private static string ResolveContentType(Type? bodyType, string declaredContentType)
    {
        // Only override the default: an explicitly-set content type is always respected as-is.
        if (bodyType is not null &&
            string.Equals(declaredContentType, DefaultJsonContentType, StringComparison.Ordinal) &&
            ProblemDetailsTypes.IsProblemDetails(bodyType))
        {
            return ProblemJsonContentType;
        }

        return declaredContentType;
    }

    private static OpenApiPathItem GetOrCreatePathItem(OpenApiDocument document, string path)
    {
        if (document.Paths!.TryGetValue(path, out var existing) && existing is OpenApiPathItem item)
        {
            return item;
        }

        var pathItem = new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation>(),
        };

        document.Paths[path] = pathItem;
        return pathItem;
    }

    private static HttpMethod ParseHttpMethod(string verb) =>
        string.IsNullOrWhiteSpace(verb) ? HttpMethod.Get : HttpMethod.Parse(verb.Trim());
}
