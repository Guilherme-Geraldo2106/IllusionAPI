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
            string path = context.Request.Path.Value ?? string.Empty;
            string method = context.Request.Method;

            Response? routeResponse = GetRouteResponse(path, method, out Dictionary<string, string>? parameters);

            if (routeResponse != null)
            {
                if (routeResponse.Body == null)
                {
                    await WriteErrorResponse(context, 500, "Invalid mock response: Body is null.");
                    return;
                }

                string serializedResponse = SerializeBody(routeResponse.Body, parameters);

                if (string.IsNullOrEmpty(serializedResponse))
                {
                    await WriteErrorResponse(context, 500, "Error serializing response body.");
                    return;
                }

                await WriteSuccessResponse(context, routeResponse.Status, serializedResponse);
            }
            else
            {
                await _next(context);
            }
        }

        private Response? GetRouteResponse(string path, string method, out Dictionary<string, string>? parameters)
        {
            parameters = null;

            foreach (IllusionRoute route in _routeConfig.IllusionRoutes)
            {
                if (string.Equals(route.Method, method, StringComparison.OrdinalIgnoreCase) &&
                    TryMatchRoute(route.Path, path, out parameters))
                {
                    return route.Response;
                }
            }

            return null;
        }

        private bool TryMatchRoute(string routeTemplate, string requestPath, out Dictionary<string, string>? parameters)
        {
            parameters = new Dictionary<string, string>();
            string[] routeSegments = routeTemplate.Split('/', StringSplitOptions.RemoveEmptyEntries);
            string[] pathSegments = requestPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (routeSegments.Length != pathSegments.Length)
                return false;

            for (int i = 0; i < routeSegments.Length; i++)
            {
                if (routeSegments[i].StartsWith("{") && routeSegments[i].EndsWith("}"))
                {
                    string paramName = routeSegments[i].Trim('{', '}');
                    parameters[paramName] = pathSegments[i];
                }
                else if (!string.Equals(routeSegments[i], pathSegments[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private string SerializeBody(object body, Dictionary<string, string>? parameters)
        {
            try
            {
                string serializedBody = JsonConvert.SerializeObject(body);

                if (parameters != null)
                {
                    foreach (KeyValuePair<string, string> parameter in parameters)
                    {
                        serializedBody = serializedBody.Replace($"{{{parameter.Key}}}", parameter.Value);
                    }
                }

                return serializedBody;
            }
            catch
            {
                return string.Empty;
            }
        }

        private async Task WriteErrorResponse(HttpContext context, int statusCode, string errorMessage)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(errorMessage);
        }

        private async Task WriteSuccessResponse(HttpContext context, int statusCode, string responseBody)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(responseBody);
        }
    }
}
