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

                var bodySections = routeSection.GetSection("response:body").GetChildren().ToList();

                if (!bodySections.Any())
                    continue;

                bool isArray = bodySections.First().GetChildren().Any();
                object formattedBody = isArray ? FormatBodyAsArray(bodySections) : FormatBodyAsObject(bodySections);

                illusionRoute.Response.Body = formattedBody;
            }
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
        public Response Response { get; set; }
    }

    public class Response
    {
        public int Status { get; set; }
        public object Body { get; set; }
    }
}

