using ZXing;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Drawing;

namespace VendorSRM_Application.Controllers.API
{
    public class BarCodeController : ApiController
    {
        public HttpResponseMessage Get(string no, string bora = null)
        {
            try
            {
                if (string.IsNullOrEmpty(no))
                {
                    throw new ArgumentException("Barcode number is required.");
                }

                // Generate the barcode
                var barcodeWriter = new BarcodeWriter
                {
                    Format = BarcodeFormat.CODE_128, // Changed to CODE_128 for barcode
                    Options = new ZXing.Common.EncodingOptions
                    {
                        Width = 400,
                        Height = 150,  // Barcode height typically smaller than QR code
                        Margin = 10,
                        PureBarcode = true
                    },
                    Renderer = new ZXing.Rendering.BitmapRenderer()
                };

                using (var bitmap = barcodeWriter.Write(no))
                using (var ms = new MemoryStream())
                {
                    // Create a new bitmap with extra space at the bottom for:
                    // 1) HU number line
                    // 2) Bora number line (if provided)
                    var extraHeight = 70; // enough space for two text lines
                    using (var finalBitmap = new Bitmap(bitmap.Width, bitmap.Height + extraHeight))
                    using (var g = Graphics.FromImage(finalBitmap))
                    {
                        g.Clear(Color.White);

                        // Draw the barcode at the top
                        g.DrawImage(bitmap, 0, 0);

                        // Prepare text:
                        // line 1: HU number
                        // line 2: Bora number (if provided)
                        var line1 = no;
                        var line2 = string.IsNullOrEmpty(bora) ? null : bora;

                        using (var font = new Font("Arial", 14, FontStyle.Regular))
                        using (var format = new StringFormat { Alignment = StringAlignment.Center })
                        {
                            var centerX = finalBitmap.Width / 2f;
                            var yBase = bitmap.Height + 5;

                            if (!string.IsNullOrEmpty(line1))
                            {
                                g.DrawString(line1, font, Brushes.Black, new PointF(centerX, yBase), format);
                            }

                            if (!string.IsNullOrEmpty(line2))
                            {
                                g.DrawString(line2, font, Brushes.Black, new PointF(centerX, yBase + 22), format);
                            }
                        }

                        finalBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    }

                    ms.Position = 0; // Reset stream position

                    // Create HttpResponseMessage with the barcode image including text
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(ms.ToArray())
                    };

                    // Set the content type and the file name
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                    response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                    {
                        FileName = "barcode.png"
                    };

                    return response;
                }
            }
            catch (Exception ex)
            {
                // Handle error and generate a text file with the exception message
                var errorMessage = $"Error occurred while generating Barcode: {ex.Message}";

                // Create a MemoryStream to hold the error message as text
                using (var ms = new MemoryStream())
                using (var writer = new StreamWriter(ms))
                {
                    writer.WriteLine(errorMessage);  // Write the error message
                    writer.Flush();
                    ms.Position = 0; // Reset stream position

                    // Create HttpResponseMessage with the error text file
                    var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        Content = new ByteArrayContent(ms.ToArray())
                    };

                    // Set the content type and the file name for the error message
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
                    response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                    {
                        FileName = "error.txt"
                    };

                    return response;
                }
            }
        }
        // GET: Generate Barcode as File
        //public HttpResponseMessage Get(string no)
        //{
        //    try
        //    {
        //        if (string.IsNullOrEmpty(no))
        //        {
        //            throw new ArgumentException("Barcode number is required.");
        //        }

        //        // Generate the barcode
        //        var barcodeWriter = new BarcodeWriter
        //        {
        //            Format = BarcodeFormat.CODE_128, // Changed to CODE_128 for barcode
        //            Options = new ZXing.Common.EncodingOptions
        //            {
        //                Width = 400,
        //                Height = 150,  // Barcode height typically smaller than QR code
        //                Margin = 10
        //            },
        //            Renderer = new ZXing.Rendering.BitmapRenderer()
        //        };

        //        using (var bitmap = barcodeWriter.Write(no))
        //        {
        //            using (var ms = new MemoryStream())
        //            {
        //                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        //                ms.Position = 0; // Reset stream position

        //                // Create HttpResponseMessage with the barcode image
        //                var response = new HttpResponseMessage(HttpStatusCode.OK)
        //                {
        //                    Content = new ByteArrayContent(ms.ToArray())
        //                };

        //                // Set the content type and the file name
        //                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        //                response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
        //                {
        //                    FileName = "barcode.png"
        //                };

        //                return response;
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        // Handle error and generate a text file with the exception message
        //        var errorMessage = $"Error occurred while generating Barcode: {ex.Message}";

        //        // Create a MemoryStream to hold the error message as text
        //        using (var ms = new MemoryStream())
        //        {
        //            using (var writer = new StreamWriter(ms))
        //            {
        //                writer.WriteLine(errorMessage);  // Write the error message
        //                writer.Flush();
        //                ms.Position = 0; // Reset stream position

        //                // Create HttpResponseMessage with the error text file
        //                var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        //                {
        //                    Content = new ByteArrayContent(ms.ToArray())
        //                };

        //                // Set the content type and the file name for the error message
        //                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        //                response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
        //                {
        //                    FileName = "error.txt"
        //                };

        //                return response;
        //            }
        //        }
        //    }
        //}
    }
}
