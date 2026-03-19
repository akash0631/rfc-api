using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace Vendor_SRM_Routing_Application.Controllers
{
    /// <summary>
    /// RfcDocUploadController - Upload RFC docs, generate controllers, push to Finaltest, publish to IIS.
    /// GET  /RfcDocUpload          -> Index()
    /// POST /RfcDocUpload/Upload   -> Upload()     Parse doc, generate C# controller
    /// GET  /RfcDocUpload/Check    -> CheckFile()  Check if exists on Finaltest
    /// POST /RfcDocUpload/Push     -> Push()       Push to Finaltest + update Swagger
    /// POST /RfcDocUpload/Publish  -> Publish()    Trigger IIS deploy (Dev or Prod)
    /// GET  /RfcDocUpload/Swagger  -> Swagger()    Swagger preview UI
    /// GET  /RfcDocUpload/SwaggerJson -> SwaggerJson() OpenAPI 3.0 spec
    /// </summary>
    public class RfcDocUploadController : System.Web.Mvc.Controller
    {
        private const string GH_API_BASE = "https://api.github.com";
        private const string GH_REPO     = "akash0631/rfc-api";
        private const string GH_BRANCH   = "Finaltest";
        private const string WORKER_URL  = "https://v2-rfc-pipeline.akash-bab.workers.dev";
        private const string IIS_BASE    = "http://v2retail.net:9005";
        // IIS Publish Settings
        private const string MSDEPLOY_URL  = "https://V2DC-ADDVERB:8172/msdeploy.axd";
        private const string SITE_DEV      = "FMS_PUTWAY_API";       // Dev:  192.168.151.36:8016
        private const string SITE_PROD     = "VendrSrmAndroidApi";   // Prod: 192.168.151.36:9005
        private const string URL_DEV       = "http://192.168.151.36:8016/";
        private const string URL_PROD      = "http://192.168.151.36:9005/";

        public ActionResult Index() { ViewBag.Title="V2 Retail - RFC Document Upload"; ViewBag.GhRepo=GH_REPO; ViewBag.GhBranch=GH_BRANCH; ViewBag.IISBase=IIS_BASE; return View(); }
        public ActionResult Swagger() { ViewBag.Title="V2 Retail - RFC Swagger Preview"; ViewBag.OpenApiUrl=Url.Action("SwaggerJson","RfcDocUpload",null,Request.Url.Scheme); return View(); }
        [HttpGet] public ActionResult SwaggerJson(string rfcName="RFC",string folder="Finance",string desc="SAP RFC") { return Content(BuildSwaggerSpec(rfcName,folder,desc),"application/json"); }

        [HttpPost]
        public async Task<ActionResult> Upload(HttpPostedFileBase file, string folder="Finance", string env="dev")
        {
            if(file==null||file.ContentLength==0) return Json(new{success=false,error="No file uploaded."});
            var allowed=new[]{"docx","txt","md"};
            var ext=Path.GetExtension(file.FileName).ToLowerInvariant().TrimStart('.');
            if(Array.IndexOf(allowed,ext)<0) return Json(new{success=false,error="Use .docx, .txt, or .md"});
            try{
                var bytes=new byte[file.ContentLength]; file.InputStream.Read(bytes,0,file.ContentLength);
                string docContent=(ext=="txt"||ext=="md")?Encoding.UTF8.GetString(bytes):await SendToWorker(bytes,file.FileName);
                var rfcName=Path.GetFileNameWithoutExtension(file.FileName).ToUpperInvariant();
                var code2=GenerateController(rfcName,docContent,folder,env);
                var path="Controllers/"+folder+"/"+rfcName+"Controller.cs";
                var desc=ExtractDescription(rfcName,docContent);
                return Json(new{success=true,rfcName,folder,filePath=path,controllerCode=code2,description=desc,
                    swaggerUrl=Url.Action("SwaggerJson","RfcDocUpload",new{rfcName,folder,desc},Request.Url.Scheme),
                    commitMessage="feat: "+rfcName+" controller - auto-generated via RFC upload portal (Finaltest)"});
            }catch(Exception ex){return Json(new{success=false,error=ex.Message});}
        }

        [HttpGet]
        public async Task<ActionResult> CheckFile(string filePath, string token)
        {
            if(string.IsNullOrWhiteSpace(filePath)||string.IsNullOrWhiteSpace(token)) return Json(new{exists=false},JsonRequestBehavior.AllowGet);
            try{ using(var c=GHClient(token)){ var r=await c.GetAsync(GH_API_BASE+"/repos/"+GH_REPO+"/contents/"+filePath+"?ref="+GH_BRANCH);
                if(r.IsSuccessStatusCode){var d=JsonConvert.DeserializeObject<JObject>(await r.Content.ReadAsStringAsync());return Json(new{exists=true,sha=d?["sha"]?.ToString()},JsonRequestBehavior.AllowGet);}
                return Json(new{exists=false},JsonRequestBehavior.AllowGet);}}
            catch(Exception ex){return Json(new{exists=false,error=ex.Message},JsonRequestBehavior.AllowGet);}
        }

        [HttpPost]
        public async Task<ActionResult> Push(RfcPushRequest req)
        {
            if(req==null||string.IsNullOrWhiteSpace(req.Token)) return Json(new{success=false,error="Token required."});
            if(string.IsNullOrWhiteSpace(req.FilePath)||string.IsNullOrWhiteSpace(req.Code)) return Json(new{success=false,error="FilePath and Code required."});
            try{
                using(var client=GHClient(req.Token)){
                    string sha=await GetFileSha(client,req.FilePath);
                    var msg=req.CommitMessage??("feat: "+req.RfcName+" - auto-generated via RFC upload portal");
                    var result=await PushFile(client,req.FilePath,req.Code,msg,sha);
                    if(!result.Success) return Json(new{success=false,error=result.Error});
                    string swaggerMsg=await UpdateSwaggerEntry(client,req.RfcName,req.Folder??"Finance",req.Description??ExtractDescription(req.RfcName,""),req.FilePath);
                    return Json(new{success=true,action=string.IsNullOrEmpty(sha)?"created":"updated",
                        commitSha=result.CommitSha,commitUrl="https://github.com/"+GH_REPO+"/commit/"+result.FullSha,
                        fileUrl="https://github.com/"+GH_REPO+"/blob/"+GH_BRANCH+"/"+req.FilePath,
                        swaggerUrl=Url.Action("SwaggerJson","RfcDocUpload",new{rfcName=req.RfcName,folder=req.Folder,desc=req.Description},Request.Url.Scheme),
                        swaggerEntry=swaggerMsg,message="Controller pushed to Finaltest + Swagger updated!"});
                }}
            catch(Exception ex){return Json(new{success=false,error=ex.Message});}
        }

        // POST /RfcDocUpload/Publish — triggers IIS deploy via GitHub Actions repository_dispatch
        [HttpPost]
        public async Task<ActionResult> Publish(RfcPublishRequest req)
        {
            if(string.IsNullOrWhiteSpace(req.Token)) return Json(new{success=false,error="GitHub token is required."});
            if(string.IsNullOrWhiteSpace(req.Environment)) return Json(new{success=false,error="Environment required: dev or prod."});
            bool isProd = req.Environment=="prod";
            string site   = isProd ? SITE_PROD  : SITE_DEV;
            string destUrl= isProd ? URL_PROD   : URL_DEV;
            try{
                using(var client=GHClient(req.Token)){
                    var payload=new JObject{
                        ["event_type"]="iis-deploy",
                        ["client_payload"]=new JObject{
                            ["environment"]=req.Environment,
                            ["site"]=site,
                            ["publish_url"]=MSDEPLOY_URL,
                            ["dest_url"]=destUrl,
                            ["branch"]=GH_BRANCH,
                            ["rfc_name"]=req.RfcName??"manual"
                        }
                    };
                    var r=await client.PostAsync(GH_API_BASE+"/repos/"+GH_REPO+"/dispatches",
                        new StringContent(payload.ToString(),Encoding.UTF8,"application/json"));
                    if(r.IsSuccessStatusCode)
                        return Json(new{success=true,environment=req.Environment,site,destUrl,
                            message="IIS deploy triggered on "+site+" ("+destUrl+"). GitHub Actions will build and deploy in ~90s."});
                    var errBody=await r.Content.ReadAsStringAsync();
                    return Json(new{success=false,error="GitHub dispatch failed: "+errBody});
                }}
            catch(Exception ex){return Json(new{success=false,error=ex.Message});}
        }

        private string BuildSwaggerSpec(string rfcName,string folder,string desc){
            var spec=new JObject{["openapi"]="3.0.1",["info"]=new JObject{["title"]="V2 Retail - "+rfcName,["version"]="1.0.0",["description"]=desc+" | Branch: Finaltest"},
                ["servers"]=new JArray{new JObject{["url"]=IIS_BASE,["description"]="IIS Primary"}},
                ["paths"]=new JObject{["/api/"+rfcName]=new JObject{["post"]=new JObject{
                    ["tags"]=new JArray(folder),["operationId"]=rfcName,["summary"]=desc,
                    ["requestBody"]=new JObject{["required"]=true,["content"]=new JObject{["application/json"]=new JObject{["schema"]=new JObject{["type"]="object",
                        ["properties"]=new JObject{["IM_DATA"]=new JObject{["type"]="array",["items"]=new JObject{["type"]="object"}}}}}}},
                    ["responses"]=new JObject{["200"]=new JObject{["description"]="Success"},["400"]=new JObject{["description"]="SAP Error"},["500"]=new JObject{["description"]="Server Error"}}}}}};
            return spec.ToString(Formatting.Indented);
        }

        private async Task<string> GetFileSha(HttpClient client,string filePath){
            try{var r=await client.GetAsync(GH_API_BASE+"/repos/"+GH_REPO+"/contents/"+filePath+"?ref="+GH_BRANCH);
                if(r.IsSuccessStatusCode){var d=JsonConvert.DeserializeObject<JObject>(await r.Content.ReadAsStringAsync());return d?["sha"]?.ToString();}
            }catch{}return null;
        }

        private async Task<PushResult> PushFile(HttpClient client,string filePath,string content,string message,string sha){
            var payload=new JObject{["message"]=message,["content"]=Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),["branch"]=GH_BRANCH};
            if(!string.IsNullOrEmpty(sha))payload["sha"]=sha;
            var r=await client.PutAsync(GH_API_BASE+"/repos/"+GH_REPO+"/contents/"+filePath,new StringContent(payload.ToString(),Encoding.UTF8,"application/json"));
            var body=await r.Content.ReadAsStringAsync();
            if(!r.IsSuccessStatusCode){var err=JsonConvert.DeserializeObject<JObject>(body);return new PushResult{Success=false,Error=err?["message"]?.ToString()??"Push failed"}; }
            var data=JsonConvert.DeserializeObject<JObject>(body);var fullSha=data?["commit"]?["sha"]?.ToString()??"";
            return new PushResult{Success=true,FullSha=fullSha,CommitSha=fullSha.Length>=8?fullSha.Substring(0,8):fullSha};
        }

        private async Task<string> UpdateSwaggerEntry(HttpClient client,string rfcName,string folder,string desc,string controllerPath){
            try{
                var filePath="Controllers/RfcDeployController.cs";
                var r=await client.GetAsync(GH_API_BASE+"/repos/"+GH_REPO+"/contents/"+filePath+"?ref="+GH_BRANCH);
                if(!r.IsSuccessStatusCode)return"Swagger update skipped";
                var data=JsonConvert.DeserializeObject<JObject>(await r.Content.ReadAsStringAsync());
                var existingSha=data?["sha"]?.ToString();
                var existing=Encoding.UTF8.GetString(Convert.FromBase64String(data?["content"]?.ToString().Replace("\n","")??""));
                if(existing.Contains("\""+rfcName+"\""))return"Swagger entry already exists for "+rfcName;
                var newEntry="\n                new RfcEndpoint{Name=\""+rfcName+"\",Route=\"api/"+rfcName+"\",Group=\""+folder+"\",SapRfc=\""+rfcName+"\",Description=\""+desc+"\",Parameters=new List<RfcParam>{new RfcParam{Name=\"IM_DATA\",SapType=\"ZTT_IMP\",IsTable=true}},ResponseTables=new List<string>{\"MSG_TYPE\",\"MESSAGE\"},FilePath=\""+controllerPath+"\"},";
                var marker="            };\n        }";
                var updated=existing.Contains(marker)?existing.Replace(marker,newEntry+"\n            };\n        }"):existing;
                if(updated==existing)return"Swagger marker not found";
                var pushResult=await PushFile(client,filePath,updated,"feat: register "+rfcName+" in Swagger (Finaltest)",existingSha);
                return pushResult.Success?"Swagger entry added for "+rfcName:"Swagger update failed: "+pushResult.Error;
            }catch(Exception ex){return"Swagger error: "+ex.Message;}
        }

        private HttpClient GHClient(string token){var c=new HttpClient();c.DefaultRequestHeaders.Add("Authorization","token "+token);c.DefaultRequestHeaders.Add("User-Agent","V2-RFC-Portal/1.0");c.DefaultRequestHeaders.Add("Accept","application/vnd.github.v3+json");return c;}

        private async Task<string> SendToWorker(byte[] bytes,string fileName){
            try{using(var client=new HttpClient())using(var form=new MultipartFormDataContent()){var fc=new ByteArrayContent(bytes);fc.Headers.ContentType=new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");form.Add(fc,"file",fileName);form.Add(new StringContent("parse"),"mode");var r=await client.PostAsync(WORKER_URL+"/parse",form);if(r.IsSuccessStatusCode)return await r.Content.ReadAsStringAsync();}}catch{}return string.Empty;
        }

        private string ExtractDescription(string rfcName,string docContent){
            if(!string.IsNullOrWhiteSpace(docContent)){foreach(var line in docContent.Split(new[]{' \n',' \r'},StringSplitOptions.RemoveEmptyEntries)){var l=line.Trim();if(l.StartsWith("Purpose:",StringComparison.OrdinalIgnoreCase))return l.Substring(8).Trim();if(l.StartsWith("Description:",StringComparison.OrdinalIgnoreCase))return l.Substring(12).Trim();}}
            var n=rfcName.ToUpperInvariant();
            if(n.Contains("DD")&&n.Contains("UPD"))return"Update Delivery Date on PO in SAP";
            if(n.Contains("COST")&&n.Contains("UPD"))return"Update PO Cost in SAP";
            if(n.Contains("QTY")&&n.Contains("UPD"))return"Update PO Quantity in SAP";
            if(n.Contains("ADVANCE"))return"Fetch advance payment documents";
            if(n.Contains("PUTWAY"))return"Validate and save inbound putway data";
            return rfcName.Replace("_"," ").Replace("RFC","").Trim()+" via SAP RFC";
        }

        private string GenerateController(string rfcName,string docContent,string folder,string env){
            var ns="Vendor_SRM_Routing_Application.Controllers."+folder;
            var envFn=env=="quality"?"rfcConfigparametersquality":env=="production"?"rfcConfigparametersproduction":"rfcConfigparameters";
            var desc=ExtractDescription(rfcName,docContent);
            return "using SAP.Middleware.Connector;\nusing System;\nusing System.Collections.Generic;\nusing System.Net;\nusing System.Net.Http;\nusing System.Threading.Tasks;\nusing System.Web.Http;\nusing Vendor_Application_MVC.Controllers;\n\nnamespace "+ns+"\n{\n    public class "+rfcName+"Controller : BaseController\n    {\n        [HttpPost]\n        public async Task<HttpResponseMessage> Post([FromBody] "+rfcName+"Request request)\n        {\n            return await Task.Run(()=>{\n                try{\n                    if(request==null||request.IM_DATA==null||request.IM_DATA.Count==0)\n                        return Request.CreateResponse(HttpStatusCode.BadRequest,new{Status=false,Message=\"IM_DATA required.\"});\n                    RfcConfigParameters rfcPar=BaseController."+envFn+"();\n                    RfcDestination dest=RfcDestinationManager.GetDestination(rfcPar);\n                    IRfcFunction myfun=dest.Repository.CreateFunction(\""+rfcName+"\");\n                    IRfcTable imData=myfun.GetTable(\"IM_DATA\");\n                    foreach(var row in request.IM_DATA){imData.Append();foreach(var kv in row)imData.SetValue(kv.Key,kv.Value??string.Empty);}\n                    myfun.Invoke(dest);\n                    string msgType=myfun.GetValue(\"MSG_TYPE\")?.ToString()??string.Empty;\n                    string message=myfun.GetValue(\"MESSAGE\")?.ToString()??string.Empty;\n                    if(msgType==\"E\")return Request.CreateResponse(HttpStatusCode.BadRequest,new{Status=false,MsgType=msgType,Message=message});\n                    return Request.CreateResponse(HttpStatusCode.OK,new{Status=true,MsgType=msgType,Message=message});\n                }catch(Exception ex){return Request.CreateResponse(HttpStatusCode.InternalServerError,new{Status=false,Message=ex.Message});}\n            });\n        }\n    }\n    public class "+rfcName+"Request{public List<Dictionary<string,string>> IM_DATA{get;set;}}\n}";
        }

        private class PushResult{public bool Success{get;set;}public string FullSha{get;set;}public string CommitSha{get;set;}public string Error{get;set;}}
    }

    public class RfcPushRequest{public string Token{get;set;}public string FilePath{get;set;}public string Code{get;set;}public string RfcName{get;set;}public string Folder{get;set;}public string Description{get;set;}public string CommitMessage{get;set;}}

    public class RfcPublishRequest{public string Token{get;set;}public string Environment{get;set;}public string RfcName{get;set;}}
}
