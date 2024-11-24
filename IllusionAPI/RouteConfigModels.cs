using Newtonsoft.Json;
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
                string routePath = routeSection.GetSection("path").Value;
                string routeMethod = routeSection.GetSection("method").Value;
                IllusionRoute illusionRoute = IllusionRoutes.SingleOrDefault(p => p.Path.Equals(routePath) && p.Method.Equals(routeMethod));

                List<IConfigurationSection> bodySections = routeSection.GetSection("response:body").GetChildren().ToList();

                if (bodySections.Any())
                {
                    var dictionary = bodySections.ToDictionary(
                        section => section.Key,
                        section => section.Value
                    );

                    string json = JsonConvert.SerializeObject(dictionary, Formatting.Indented);
                    illusionRoute.Response.Body = JsonConvert.DeserializeObject<object>(json);
                }
            }
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

