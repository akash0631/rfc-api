using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Web.Http.Cors;

namespace Vendor_SRM_Routing_Application
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // ── Request logging: captures every API call with timing and status ──
            config.MessageHandlers.Add(new Vendor_SRM_Routing_Application.Utils.RequestLoggingHandler());

            // Enable CORS for all origins, all headers, and all methods
            var cors = new EnableCorsAttribute("*", "*", "*");
            config.EnableCors(cors);

            // Other Web API configuration and services
            config.MapHttpAttributeRoutes();
            

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }
}
