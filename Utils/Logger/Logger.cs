using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace Vendor_SRM_Routing_Application.Utils.Logger
{
    public static class LogHelper
    {
        private static readonly string baseLogsFolderPath;

        static LogHelper()
        {
            // Get the base directory for logs in wwwroot/Logs
            //string appRoot = HttpRuntime.AppDomainAppPath;
            baseLogsFolderPath = Path.Combine("D:\\Logs", "wwwroot", "Logs");

            // Ensure Logs directory exists
            Directory.CreateDirectory(baseLogsFolderPath);
        }

        public static void WriteLog(string message, string projectName="Unknown Project",string fileName = "",string Type="Success")
        {
            // Generate timestamp components
            string year = DateTime.Now.ToString("yyyy");
            string month = DateTime.Now.ToString("MM");
            string day = DateTime.Now.ToString("dd");
            string timestamp = DateTime.Now.ToString("HHmmssfff");

            // Sanitize project name (remove invalid characters)
            string safeProjectName = string.Concat(projectName.Split(Path.GetInvalidFileNameChars()));

            // Define dynamic folder path (Year/Month/Day/ProjectName)
            string logsFolderPath = Path.Combine(baseLogsFolderPath, year, month, day, safeProjectName,Type);

            // Ensure directory structure exists
            Directory.CreateDirectory(logsFolderPath);

            // Define complete file path
            string filePath = Path.Combine(logsFolderPath, $"{fileName}{timestamp}.txt");

            // Write message to the file
            File.WriteAllText(filePath, message);
        }
    }

}