using System;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Vendor_SRM_Routing_Application.Services;

namespace Vendor_SRM_Routing_Application
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            SwaggerConfig.Register();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // ── Hybrid Phase 2: start background sync scheduler ──────────────
            // Reads GOLD.RFC_SYNC_JOB from Snowflake and auto-syncs SAP → GOLD on timers.
            // Graceful: if RFC_SYNC_JOB table doesn't exist yet, scheduler starts with 0 jobs.
            try { DataSyncScheduler.Instance.Start(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[Startup] DataSyncScheduler failed to start: " + ex.Message);
            }
        }

        protected void Application_End()
        {
            try { DataSyncScheduler.Instance.Stop(); } catch { }
        }
    }
}
