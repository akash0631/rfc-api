using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Vendor_Application_MVC.Models
{
   
    public class Authenticate
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public string StoreId { get; set; }
        public List<AuthenticateResponse> Data;
        public Authenticate()
        {
            Data = new List<AuthenticateResponse>();
        }
    }
    public class Authenticate<T>
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public List<T> Data;
        public Authenticate()
        {
            Data = new List<T>();
        }
    }
    public class AuthenticateRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }

    }
    public class AuthenticateResponse
    {
        public string Vendor_Code { get; set; }
        public string Vendor_Name { get; set; }
        public string PO_Number { get; set; }
        public string Date { get; set; } = String.Empty;
        public string DeliveryDate { get; set; } = String.Empty;
        public string POQty { get; set; } = String.Empty;
        public string Site { get; set; } = String.Empty;
    }

    public class ZoneAuthenticateResponse
    {
        public string Account_Number { get; set; }
        public string Vendor_Name1 { get; set; }
        public string ZoneId { get; set; }
        public string ZoneState { get; set; } = String.Empty;
        public string Vendor_Name { get; set; } = String.Empty;
    }
    public class ZoneVendorPoResponse
    {
        public string Vendor_Code { get; set; }
        public string Vendor_Name { get; set; }
        public string PO_Number { get; set; }
        public string EBelp { get; set; } = String.Empty;
        public string Menge { get; set; } = String.Empty;
    }
}