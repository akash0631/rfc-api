using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using Vendor_SRM_Routing_Application.Models.Login;

namespace Vendor_SRM_Routing_Application.Controllers.Authentication
{
    public class LoginController:BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] LoginRequest request)
        {
            if(request.username == "testing@v2kart.com" || request.password == "123456")
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = true,
                    Message = "Logged In Successfylly"
                });
            } else
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = false,
                    Message = "Username or Password is Invalid"
                });
            }
        }
    }
}