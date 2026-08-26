using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Azure.Functions.Worker;

namespace Azure.Functions.OpenApi.Discovery;

/// <summary>
/// The raw result of reflecting over a single HTTP-triggered Azure Functions method.
/// </summary>
/// <param name="Path">
/// The effective request path (leading <c>/</c>, no double slashes), combining the host route
/// prefix with the trigger route template (or the function name when no route is specified).
/// </param>
/// <param name="HttpMethods">The upper-cased HTTP verbs the trigger accepts.</param>
/// <param name="Method">The reflected method that declares the <see cref="FunctionAttribute"/>.</param>
/// <param name="RouteParameters">
/// The names of route-template parameters (constraints and optional markers stripped).
/// </param>
/// <remarks>
/// This is deliberately a low-level discovery record: it surfaces what reflection found without
/// building any OpenAPI objects. Turning this into an <c>OpenApiPaths</c> entry is the
/// paths-builder's job.
/// </remarks>
internal sealed record DiscoveredEndpoint(
    string Path,
    IReadOnlyList<string> HttpMethods,
    MethodInfo Method,
    IReadOnlyList<string> RouteParameters);

/// <summary>
/// Discovers HTTP-triggered Azure Functions endpoints by reflecting over candidate assemblies.
/// </summary>
/// <remarks>
/// The discovery is decoupled from <see cref="OpenApiOptions"/>: callers pass the assemblies to
/// scan and the route prefix explicitly, so this type does not depend on options the Lead is still
/// wiring up. Discovery is resilient: a single malformed method is skipped rather than aborting the
/// whole scan.
/// </remarks>
internal sealed class FunctionEndpointDiscovery
{
    private static readonly string[] FrameworkAssemblyPrefixes =
    [
        "System",
        "Microsoft",
        "netstandard",
        "mscorlib",
    ];

    // Matches route tokens like {id}, {id:int}, {id?}, {id:int?}, capturing only the name.
    private static readonly Regex RouteParameterRegex = new(
        @"\{(?<name>[A-Za-z_][A-Za-z0-9_]*)(?::[^}]*)?\??\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Discovers all HTTP-triggered function endpoints in the given assemblies.
    /// </summary>
    /// <param name="assemblies">
    /// The assemblies to scan. When empty, a default set is resolved via
    /// <see cref="GetDefaultAssemblies"/>.
    /// </param>
    /// <param name="routePrefix">
    /// The host route prefix (for example <c>api</c>). May be empty to serve from the root.
    /// </param>
    /// <returns>The discovered endpoints, in discovery order.</returns>
    public IReadOnlyList<DiscoveredEndpoint> Discover(IEnumerable<Assembly> assemblies, string routePrefix)
    {
        var candidates = (assemblies ?? []).Where(static a => a is not null).Distinct().ToList();
        if (candidates.Count == 0)
        {
            candidates = GetDefaultAssemblies().ToList();
        }

        var results = new List<DiscoveredEndpoint>();

        foreach (var assembly in candidates)
        {
            foreach (var type in GetTypesSafe(assembly))
            {
                foreach (var method in GetMethodsSafe(type))
                {
                    var endpoint = TryDiscover(method, routePrefix);
                    if (endpoint is not null)
                    {
                        results.Add(endpoint);
                    }
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Resolves the default assemblies to scan: the entry assembly plus loaded, non-framework
    /// assemblies (those whose simple name does not start with a known framework prefix).
    /// </summary>
    public IEnumerable<Assembly> GetDefaultAssemblies()
    {
        var seen = new HashSet<Assembly>();
        var result = new List<Assembly>();

        var entry = Assembly.GetEntryAssembly();
        if (entry is not null && seen.Add(entry))
        {
            result.Add(entry);
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic || IsFrameworkAssembly(assembly))
            {
                continue;
            }

            if (seen.Add(assembly))
            {
                result.Add(assembly);
            }
        }

        return result;
    }

    private static DiscoveredEndpoint? TryDiscover(MethodInfo method, string routePrefix)
    {
        try
        {
            var functionAttribute = method.GetCustomAttribute<FunctionAttribute>();
            if (functionAttribute is null)
            {
                return null;
            }

            var httpTrigger = FindHttpTrigger(method);
            if (httpTrigger is null)
            {
                return null;
            }

            var methods = (httpTrigger.Methods ?? [])
                .Where(static m => !string.IsNullOrWhiteSpace(m))
                .Select(static m => m.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();

            var template = string.IsNullOrWhiteSpace(httpTrigger.Route)
                ? functionAttribute.Name
                : httpTrigger.Route!;

            var path = CombinePath(routePrefix, template);
            var routeParameters = ExtractRouteParameters(template);

            return new DiscoveredEndpoint(path, methods, method, routeParameters);
        }
        catch
        {
            // A malformed endpoint must never abort discovery of the rest.
            return null;
        }
    }

    private static HttpTriggerAttribute? FindHttpTrigger(MethodInfo method)
    {
        foreach (var parameter in method.GetParameters())
        {
            var trigger = parameter.GetCustomAttribute<HttpTriggerAttribute>();
            if (trigger is not null)
            {
                return trigger;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ExtractRouteParameters(string? template)
    {
        if (string.IsNullOrEmpty(template))
        {
            return [];
        }

        var names = new List<string>();
        foreach (Match match in RouteParameterRegex.Matches(template))
        {
            var name = match.Groups["name"].Value;
            if (!string.IsNullOrEmpty(name) && !names.Contains(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    private static string CombinePath(string? routePrefix, string? route)
    {
        var normalizedRoute = NormalizeRouteTokens(route);

        var segments = new[] { routePrefix, normalizedRoute }
            .SelectMany(static s => (s ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries));

        return "/" + string.Join('/', segments);
    }

    // Rewrites {name:constraint}, {name?}, {name:constraint?} tokens to a bare {name} so the
    // emitted path matches OpenAPI path-template expectations.
    private static string NormalizeRouteTokens(string? route)
    {
        if (string.IsNullOrEmpty(route))
        {
            return string.Empty;
        }

        return RouteParameterRegex.Replace(route, static m => "{" + m.Groups["name"].Value + "}");
    }

    private static bool IsFrameworkAssembly(Assembly assembly)
    {
        var name = assembly.GetName().Name;
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        foreach (var prefix in FrameworkAssemblyPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Type> GetTypesSafe(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(static t => t is not null)!;
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<MethodInfo> GetMethodsSafe(Type type)
    {
        try
        {
            return type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        }
        catch
        {
            return [];
        }
    }
}
