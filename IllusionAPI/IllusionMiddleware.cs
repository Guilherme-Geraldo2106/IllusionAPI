using Newtonsoft.Json;
using System.Text;
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

            // Busca as respostas configuradas para a rota
            List<Response>? routeResponses = GetRouteResponse(path, method, out Dictionary<string, string>? parameters);

            if (routeResponses != null && routeResponses.Any())
            {
                object? requestBody = null;

                // Lê o corpo da requisição para métodos relevantes
                if (method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
                    method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
                    method.Equals("PATCH", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        requestBody = DeserializeRequestBody(context);
                    }
                    catch (Exception ex)
                    {
                        await WriteErrorResponse(context, 400, $"Error reading request body: {ex.Message}");
                        return;
                    }
                }

                // Avalia qual resposta deve ser retornada
                Response? matchedResponse = EvaluateResponseConditions(routeResponses, requestBody);

                if (matchedResponse != null)
                {
                    // Substitui os parâmetros dinâmicos no corpo da resposta
                    string serializedResponse = SerializeBody(matchedResponse.Body, parameters);

                    if (string.IsNullOrEmpty(serializedResponse))
                    {
                        await WriteErrorResponse(context, 500, "Error serializing response body.");
                        return;
                    }

                    await WriteSuccessResponse(context, matchedResponse.Status, serializedResponse);
                }
                else
                {
                    await WriteErrorResponse(context, 400, "No matching condition found for the provided request.");
                }
            }
            else
            {
                await _next(context); // Passa para o próximo middleware se a rota não for tratada
            }
        }

        private Dictionary<string, object> DeserializeRequestBody(HttpContext context)
        {
            context.Request.EnableBuffering(); // Permite ler o corpo da requisição várias vezes
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            string body = reader.ReadToEndAsync().Result;
            context.Request.Body.Position = 0; // Reseta o stream após leitura
            return JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
        }

        private Response? EvaluateResponseConditions(List<Response> responses, object? requestBody)
        {
            foreach (var response in responses)
            {
                // Sem condição definida, retorna como padrão
                if (string.IsNullOrWhiteSpace(response.Condition))
                    return response;

                // Avalia a condição
                if (EvaluateCondition(response.Condition, requestBody))
                    return response;
            }

            return null; // Nenhuma condição satisfeita
        }

        private bool EvaluateCondition(string condition, object? requestBody)
        {
            if (requestBody is not Dictionary<string, object> requestBodyDict)
                return false;

            // Divide a condição com base nos operadores lógicos "&&" e "||"
            var conditionParts = condition.Split(new[] { "&&", "||" }, StringSplitOptions.None)
                                          .Select(part => part.Trim())
                                          .ToArray();

            bool result = false;
            bool isFirstCondition = true;

            foreach (var part in conditionParts)
            {
                // Avalia a condição dentro de cada parte
                var conditionEvaluation = EvaluateSingleCondition(part, requestBodyDict);

                // Compara a avaliação com o operador lógico
                if (isFirstCondition)
                {
                    result = conditionEvaluation;
                    isFirstCondition = false;
                }
                else
                {
                    if (condition.Contains("&&"))
                    {
                        result = result && conditionEvaluation;
                    }
                    else if (condition.Contains("||"))
                    {
                        result = result || conditionEvaluation;
                    }
                }
            }

            return result;
        }

        private bool EvaluateSingleCondition(string condition, Dictionary<string, object> requestBodyDict)
        {
            // Suporta condições como: "field == 'value'", "field != 'value'", "field > 10"
            var conditionParts = condition.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (conditionParts.Length != 3)
                return false; // Condições mal formatadas são ignoradas

            var fieldName = conditionParts[0];
            var operatorType = conditionParts[1];
            var expectedValue = conditionParts[2].Trim('\'');

            if (!requestBodyDict.TryGetValue(fieldName, out var actualValue))
                return false;

            // Convertendo valor esperado para o tipo de dado do valor real
            var actualValueStr = actualValue?.ToString();

            if (double.TryParse(actualValueStr, out double actualNumericValue) &&
                double.TryParse(expectedValue, out double expectedNumericValue))
            {
                // Condições numéricas
                return operatorType switch
                {
                    "==" => actualNumericValue == expectedNumericValue,
                    "!=" => actualNumericValue != expectedNumericValue,
                    ">" => actualNumericValue > expectedNumericValue,
                    "<" => actualNumericValue < expectedNumericValue,
                    ">=" => actualNumericValue >= expectedNumericValue,
                    "<=" => actualNumericValue <= expectedNumericValue,
                    _ => false
                };
            }

            // Condições de string
            return operatorType switch
            {
                "==" => actualValueStr == expectedValue,
                "!=" => actualValueStr != expectedValue,
                _ => false
            };
        }


        private List<Response>? GetRouteResponse(string path, string method, out Dictionary<string, string>? parameters)
        {
            parameters = null;

            foreach (IllusionRoute route in _routeConfig.IllusionRoutes)
            {
                if (string.Equals(route.Method, method, StringComparison.OrdinalIgnoreCase) &&
                    TryMatchRoute(route.Path, path, out parameters))
                {
                    return route.Responses;
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

