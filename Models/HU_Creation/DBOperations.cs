using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace VendorSRM_Application.Models
{
    public class DBOperations
    {
        public readonly string connectionString = ConfigurationManager.ConnectionStrings["HuCreation"].ConnectionString;
        
    }
}