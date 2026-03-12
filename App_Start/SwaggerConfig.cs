using Swashbuckle.Application;
using System.Web.Http;

public class SwaggerConfig
{
    public static void Register()
    {
        GlobalConfiguration.Configuration
            .EnableSwagger(c => c.SingleApiVersion("v1", "My API"))  // Set version and title
            .EnableSwaggerUi();  // Enable the Swagger UI
    }
}