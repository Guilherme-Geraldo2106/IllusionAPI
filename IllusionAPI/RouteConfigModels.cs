using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IllusionAPI
{
    public class RouteConfig
    {
        public List<IllusionRoute> IllusionRoutes { get; set; }

        private void FormatBodyJson(List<IConfigurationSection> illusionRoutesSection)
        {
            foreach (IConfigurationSection routeSection in illusionRoutesSection)
            {
                string routePath = routeSection.GetValue<string>("path");
                string routeMethod = routeSection.GetValue<string>("method");

                IllusionRoute? illusionRoute = IllusionRoutes.FirstOrDefault(
                    p => p.Path.Equals(routePath, StringComparison.OrdinalIgnoreCase) &&
                         p.Method.Equals(routeMethod, StringComparison.OrdinalIgnoreCase));

                if (illusionRoute == null)
                    continue;

                var requestBodySections = routeSection.GetSection("requestBody")?.GetChildren().ToList();
                if (requestBodySections.Any())
                {
                    illusionRoute.RequestBody = FormatBodyAsObject(requestBodySections);
                }

                var responseSection = routeSection.GetSection("response");
                if (responseSection.Exists())
                {
                    if (responseSection.GetChildren().Any(child => child.Key == "status"))
                    {
                        illusionRoute.Responses = new List<Response>
                    {
                        ParseSingleResponse(responseSection)
                    };
                    }
                    else
                    {
                        var responseListSections = responseSection.GetChildren().ToList();
                        if (responseListSections.Any())
                        {
                            illusionRoute.Responses = responseListSections
                                .Select(ParseSingleResponse)
                                .ToList();
                        }
                    }
                }
            }
        }

        private Response ParseSingleResponse(IConfigurationSection responseSection)
        {
            var status = responseSection.GetValue<int>("status");
            var condition = responseSection.GetValue<string>("condition", string.Empty);
            var bodySections = responseSection.GetSection("body")?.GetChildren().ToList();

            object formattedBody = bodySections.Any() && bodySections.First().GetChildren().Any()
                ? FormatBodyAsArray(bodySections)
                : FormatBodyAsObject(bodySections);

            return new Response
            {
                Status = status,
                Condition = condition,
                Body = formattedBody
            };
        }

        private object FormatBodyAsArray(List<IConfigurationSection> bodySections)
        {
            var bodyList = bodySections
                .Select(section => section.GetChildren().ToDictionary(child => child.Key, child => child.Value))
                .ToList();

            return DeserializeJson(bodyList);
        }

        private object FormatBodyAsObject(List<IConfigurationSection> bodySections)
        {
            var bodyObject = bodySections.ToDictionary(section => section.Key, section => section.Value);
            return DeserializeJson(bodyObject);
        }

        private object DeserializeJson(object input)
        {
            string json = JsonConvert.SerializeObject(input, Formatting.Indented);
            return JsonConvert.DeserializeObject<object>(json) ?? new object();
        }

        public static RouteConfig GenRouteConfig(IConfigurationSection irouteConfSection)
        {
            RouteConfig routeConfig = irouteConfSection.Get<RouteConfig>();
            routeConfig.FormatBodyJson(irouteConfSection.GetSection("illusionRoutes").GetChildren().ToList());
            return routeConfig;
        }
    }



    public class IllusionRoute
    {
        public string Method { get; set; }
        public string Path { get; set; }
        public object? RequestBody { get; set; }
        public List<Response> Responses { get; set; }
    }

    public class Response
    {
        public int Status { get; set; }
        public string Condition { get; set; }
        public object Body { get; set; }
    }
}

