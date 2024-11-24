//using IllusionAPI;

//var builder = WebApplication.CreateBuilder(args);

//builder.Configuration.SetBasePath(builder.Environment.ContentRootPath)
//                     .AddJsonFile("routes.json", optional: false, reloadOnChange: true);

//builder.Services.AddSingleton<RouteConfig>(sp => RouteConfig.GenRouteConfig(builder.Configuration.GetSection("illusionRouteConfig")));

//builder.Services.AddSwaggerGen(options =>
//{
//    options.TagActionsBy(api =>
//    {
//        if (api.HttpMethod != null)
//        {
//            return new[] { api.HttpMethod.ToUpper() }; 
//        }
//        return new[] { "default" }; 
//    });

//    options.DocumentFilter<DynamicRoutesDocumentFilter>();
//});

//WebApplication app = builder.Build();

//app.UseSwagger();
//app.UseSwaggerUI();

//RouteConfig routeConfig = app.Services.GetRequiredService<RouteConfig>();

//foreach (IllusionRoute route in routeConfig.IllusionRoutes)
//{
//    IEndpointConventionBuilder routeBuilder = route.Method switch
//    {
//        "GET" => app.MapGet(route.Path, () => Results.Json(route.Responses.Body)),
//        "POST" => app.MapPost(route.Path, (HttpRequest request) => Results.Json(route.Responses.Body)),
//        "PUT" => app.MapPut(route.Path, (HttpRequest request) => Results.Json(route.Responses.Body)),
//        "DELETE" => app.MapDelete(route.Path, () => Results.Json(route.Responses.Body)),
//        _ => null
//    };
//}

//app.UseMiddleware<IllusionMiddleware>();

//app.Run();

using IllusionAPI;

var builder = WebApplication.CreateBuilder(args);

// Carrega a configuração das rotas
builder.Configuration.SetBasePath(builder.Environment.ContentRootPath)
                     .AddJsonFile("routes.json", optional: false, reloadOnChange: true);

// Injeta a configuração das rotas no DI container
builder.Services.AddSingleton<RouteConfig>(sp =>
    RouteConfig.GenRouteConfig(builder.Configuration.GetSection("illusionRouteConfig")));

// Configura o Swagger
builder.Services.AddSwaggerGen(options =>
{
    options.DocumentFilter<DynamicRoutesDocumentFilter>();
});

var app = builder.Build();

// Ativa o Swagger e a UI
app.UseSwagger();
app.UseSwaggerUI();

// Obtém a configuração de rotas
RouteConfig routeConfig = app.Services.GetRequiredService<RouteConfig>();

// Adiciona o middleware customizado
app.UseMiddleware<IllusionMiddleware>();

// Inicializa o app
app.Run();

