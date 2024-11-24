using IllusionAPI;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
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
            // Adiciona o path se ainda não existir
            if (!swaggerDoc.Paths.ContainsKey(route.Path))
            {
                swaggerDoc.Paths.Add(route.Path, new OpenApiPathItem());
            }

            var pathItem = swaggerDoc.Paths[route.Path];
            var operationType = ParseMethod(route.Method);

            // Adiciona a operação se ainda não existir
            if (!pathItem.Operations.ContainsKey(operationType))
            {
                var operation = new OpenApiOperation
                {
                    Summary = $"Mocked route for {route.Path}",
                    Description = "This is a dynamically mocked endpoint",
                    Responses = new OpenApiResponses
                    {
                        {
                            route.Responses[0].Status.ToString(),
                            new OpenApiResponse { Description = "Mocked response" }
                        }
                    }
                };

                // Adiciona parâmetros dinâmicos, se existirem
                AddPathParameters(operation, route.Path);

                // Adiciona request body para métodos relevantes
                if (IsRequestBodyMethod(route.Method) && route.RequestBody != null)
                {
                    operation.RequestBody = ConvertRequestBodyToOpenApi(route.RequestBody);
                }

                pathItem.Operations.Add(operationType, operation);
            }
        }
    }

    private void AddPathParameters(OpenApiOperation operation, string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
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
    }

    private OpenApiRequestBody ConvertRequestBodyToOpenApi(object requestBodyConfig)
    {
        var openApiRequestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>()
        };

        try
        {
            // Assume que requestBodyConfig é um dicionário chave-valor
            string requestBodyConfigJson = JsonConvert.SerializeObject(requestBodyConfig);
            var bodyConfig = JsonConvert.DeserializeObject<Dictionary<string, string>>(requestBodyConfigJson);

            if (bodyConfig != null && bodyConfig.Any())
            {
                var schemaProperties = new Dictionary<string, OpenApiSchema>();

                foreach (var entry in bodyConfig)
                {
                    schemaProperties.Add(entry.Key, new OpenApiSchema
                    {
                        Type = entry.Value,
                        Description = $"Field {entry.Key}"
                    });
                }

                openApiRequestBody.Content.Add("application/json", new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = schemaProperties
                    }
                });
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Invalid requestBody format in route configuration.", ex);
        }

        return openApiRequestBody;
    }

    private bool IsRequestBodyMethod(string method)
    {
        return method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
               method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
               method.Equals("PATCH", StringComparison.OrdinalIgnoreCase);
    }

    private OperationType ParseMethod(string method)
    {
        return method.ToUpper() switch
        {
            "GET" => OperationType.Get,
            "POST" => OperationType.Post,
            "PUT" => OperationType.Put,
            "DELETE" => OperationType.Delete,
            _ => throw new ArgumentException($"Invalid method type: {method}")
        };
    }
}
