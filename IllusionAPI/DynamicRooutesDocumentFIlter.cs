using IllusionAPI;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public class DynamicRoutesDocumentFilter : IDocumentFilter
{
    private readonly RouteConfig _routeConfig;

    public DynamicRoutesDocumentFilter(IServiceProvider serviceProvider)
    {
        _routeConfig = serviceProvider.GetRequiredService<RouteConfig>();
    }

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        foreach (var route in _routeConfig.IllusionRoutes)
        {
            OpenApiOperation operation = swaggerDoc.Paths[route.Path].Operations[ParseMethod(route.Method)];
            if (operation == null)
            {
                operation = new OpenApiOperation
                {
                    Summary = $"Mocked route for {route.Path}",
                    Description = "This is a dynamically mocked endpoint",
                    Responses = new OpenApiResponses
                {
                    {
                        route.Response.Status.ToString(),
                        new OpenApiResponse { Description = "Mocked response" }
                    }
                }
                };
            }


            var routeSegments = route.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            foreach (var segment in routeSegments)
            {
                if (segment.StartsWith("{") && segment.EndsWith("}"))
                {
                    var paramName = segment.Trim('{', '}');
                    operation.Parameters.Add(new OpenApiParameter
                    {
                        Name = paramName,
                        In = ParameterLocation.Path,
                        Required = true,
                        Schema = new OpenApiSchema { Type = "string" },
                        Description = $"Parameter '{paramName}' in route"
                    });
                }
            }

            if (!swaggerDoc.Paths.ContainsKey(route.Path))
            {
                swaggerDoc.Paths.Add(route.Path, new OpenApiPathItem
                {
                    Operations = { [ParseMethod(route.Method)] = operation }
                });
            }
        }
    }

    private OperationType ParseMethod(string method)
    {
        return method.ToUpper() switch
        {
            "GET" => OperationType.Get,
            "POST" => OperationType.Post,
            "PUT" => OperationType.Put,
            "DELETE" => OperationType.Delete,
            _ => OperationType.Get
        };
    }
}
