using Swashbuckle.Application;
using System.Linq;
using System.Web.Http;

/// <summary>
/// Swashbuckle Swagger configuration.
/// UI available at: /swagger/ui/index
/// JSON spec at:    /swagger/docs/v1
/// </summary>
public class SwaggerConfig
{
    public static void Register()
    {
        GlobalConfiguration.Configuration
            .EnableSwagger(c =>
            {
                c.SingleApiVersion("v1", "V2 Retail · SAP RFC API")
                    .Description("REST API wrappers for all SAP RFC functions. " +
                                 "Deployed on IIS (V2DC-ADDVERB). " +
                                 "CI/CD via GitHub Actions → deploy-iis.yml.")
                    .Contact(cc => cc
                        .Name("V2 Retail Tech")
                        .Url("https://github.com/akash0631/rfc-api"));

                // Group by first URL segment (controller name)
                c.GroupActionsBy(apiDesc =>
                    apiDesc.ActionDescriptor.ControllerDescriptor.ControllerName);

                // Sort controllers alphabetically
                c.OrderActionGroupsBy(new SwaggerGroupNameComparer());

                // Use full type names as schema IDs to avoid duplicate class-name conflicts
                c.UseFullTypeNameInSchemaIds();

                // Resolve duplicate route conflicts (multiple actions on same path+method)
                c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
            })
            .EnableSwaggerUi(ui =>
            {
                ui.DocumentTitle("V2 Retail · RFC API Swagger");
                ui.EnableDiscoveryUrlSelector();
            });
    }
}

public class SwaggerGroupNameComparer : System.Collections.Generic.IComparer<string>
{
    public int Compare(string x, string y) => string.Compare(x, y, System.StringComparison.OrdinalIgnoreCase);
}
