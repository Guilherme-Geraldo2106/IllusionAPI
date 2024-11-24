using IllusionAPI;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.SetBasePath(builder.Environment.ContentRootPath)
                     .AddJsonFile("routes.json", optional: false, reloadOnChange: true);

builder.Services.AddSingleton<RouteConfig>(sp => RouteConfig.GenRouteConfig(builder.Configuration.GetSection("illusionRouteConfig")));

builder.Services.AddSwaggerGen(options =>
{
    options.TagActionsBy(api =>
    {
        if (api.HttpMethod != null)
        {
            return new[] { api.HttpMethod.ToUpper() }; 
        }
        return new[] { "default" }; 
    });

    options.DocumentFilter<DynamicRoutesDocumentFilter>();
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

RouteConfig routeConfig = app.Services.GetRequiredService<RouteConfig>();

foreach (IllusionRoute route in routeConfig.IllusionRoutes)
{
    IEndpointConventionBuilder routeBuilder = route.Method switch
    {
        "GET" => app.MapGet(route.Path, () => Results.Json(route.Response.Body)),
        "POST" => app.MapPost(route.Path, (HttpRequest request) => Results.Json(route.Response.Body)),
        "PUT" => app.MapPut(route.Path, (HttpRequest request) => Results.Json(route.Response.Body)),
        "DELETE" => app.MapDelete(route.Path, () => Results.Json(route.Response.Body)),
        _ => null
    };
}

app.UseMiddleware<IllusionMiddleware>();

app.Run();
