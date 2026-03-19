using System.Web.Mvc;
using System.Web.Routing;

namespace Vendor_SRM_Routing_Application
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.IgnoreRoute("swagger/{*pathInfo}"); // Let Swashbuckle handle all /swagger/* routes

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new
                {
                    controller = "RfcDeploy",   // Startup page: RFC Deploy Wizard
                    action     = "Index",
                    id         = UrlParameter.Optional
                }
            );
        }
    }
}
