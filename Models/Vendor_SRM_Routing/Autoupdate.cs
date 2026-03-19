using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Vendor_Application_MVC.Models
{
   
    public class Autoupdate
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public List<AutoupdateResponse> Data;
        public Autoupdate()
        {
            Data = new List<AutoupdateResponse>();
        }
    }
    public class AutoupdateRequest
    {
        public string Version_Code { get; set; }
        public string Version_Name { get; set; }
        public string Drive_Link { get; set; }

    }
    public class AutoupdateResponse
    {
        public string Version_Code { get; set; }
        public string Version_Name { get; set; }
        public string Drive_Link { get; set; }
    }
}