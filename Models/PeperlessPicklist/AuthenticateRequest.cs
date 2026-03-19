using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Vendor_SRM_Routing_Application.Models.PeperlessPicklist
{
    public class AuthenticateRequest
    {
        public string Username { get; set; } = String.Empty;
        public string Password { get; set; } = String.Empty;
    }
}