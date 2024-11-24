using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Text.Json;

namespace IllusionAPI
{
    public class IllusionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly RouteConfig _routeConfig;

        public IllusionMiddleware(RequestDelegate next, RouteConfig routeConfig)
        {
            _next = next;
            _routeConfig = routeConfig;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            string path = context.Request.Path;
            string method = context.Request.Method;

            Response mockResponse = GetMockResponse(path, method);

            if (mockResponse != null)
            {
                context.Response.StatusCode = mockResponse.Status;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsync(JsonConvert.SerializeObject(mockResponse.Body));
            }
            else
            {
                await _next(context);
            }
        }

        private Response GetMockResponse(string path, string method)
        {
            IllusionRoute route = _routeConfig.IllusionRoutes.FirstOrDefault(r =>
                r.Path.Equals(path, StringComparison.OrdinalIgnoreCase) &&
                r.Method.Equals(method, StringComparison.OrdinalIgnoreCase));

            return route?.Response;
        }
    }


}
