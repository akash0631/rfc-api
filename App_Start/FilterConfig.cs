using System.Web;
using System.Web.Mvc;

namespace Vendor_SRM_Routing_Application
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }
}
