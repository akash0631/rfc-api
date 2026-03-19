using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FMS_Fabric_Putway_Api.Models
{
   
    public class FMSAutoUpdate
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public List<FMSAutoupdateResponse> Data;
        public FMSAutoUpdate()
        {
            Data = new List<FMSAutoupdateResponse>();
        }
    }
    public class FMSAutoupdateRequest
    {
        public string Version_Code { get; set; }
        public string Version_Name { get; set; }
        public string Drive_Link { get; set; }

    }
    public class FMSAutoupdateResponse
    {
        public string Version_Code { get; set; }
        public string Version_Name { get; set; }
        public string Drive_Link { get; set; }
    }
}