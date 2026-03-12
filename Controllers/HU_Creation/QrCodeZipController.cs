using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using ZXing;
using ZXing.Common;
using ZXing.Rendering;

namespace YourNamespace.Controllers
{
    public class QrCodeZipController : ApiController
    {
        // GET api/QrCodeZip?pairs=HU1|B1,HU2|,HU3|B3
        [HttpGet]
        public HttpResponseMessage Get(string numbers)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(numbers))
                    throw new ArgumentException("Comma-separated HU|BORA pairs are required.");

                // Parse pairs
                var groups = numbers
                    .Split(',')
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToList();

                if (groups.Count == 0)
                    throw new ArgumentException("At least one valid HU|BORA pair is required.");

                using (var zipStream = new MemoryStream())
                using (var zip = new ZipFile())
                {
                    var barcodeWriter = new BarcodeWriter
                    {
                        Format = BarcodeFormat.CODE_128,
                        Options = new EncodingOptions
                        {
                            Width = 400,
                            Height = 150,
                            Margin = 10,
                            PureBarcode = true // ✅ prevents duplicate HU text
                        },
                        Renderer = new BitmapRenderer()
                    };

                    foreach (var group in groups)
                    {
                        var parts = group.Split('|');
                        var huNumber = parts[0].Trim();
                        var bora = parts.Length > 1 ? parts[1].Trim() : null;

                        if (string.IsNullOrWhiteSpace(huNumber))
                            throw new Exception("HU number is required in each pair.");

                        using (var barcodeBitmap = barcodeWriter.Write(huNumber))
                        using (var imageStream = new MemoryStream())
                        {
                            int extraHeight = 50;

                            using (var finalBitmap = new Bitmap(barcodeBitmap.Width, barcodeBitmap.Height + extraHeight))
                            using (var g = Graphics.FromImage(finalBitmap))
                            {
                                g.Clear(Color.White);
                                g.DrawImage(barcodeBitmap, 0, 0);

                                using (var font = new Font("Arial", 14, FontStyle.Regular))
                                using (var format = new StringFormat { Alignment = StringAlignment.Center })
                                {
                                    float centerX = finalBitmap.Width / 2f;
                                    float yBase = barcodeBitmap.Height + 5;

                                    // HU number
                                    g.DrawString(huNumber, font, Brushes.Black,
                                        new PointF(centerX, yBase), format);

                                    // Bora (optional)
                                    if (!string.IsNullOrWhiteSpace(bora))
                                    {
                                        g.DrawString(bora, font, Brushes.Black,
                                            new PointF(centerX, yBase + 20), format);
                                    }
                                }

                                finalBitmap.Save(imageStream, ImageFormat.Png);
                            }

                            // Add image to ZIP
                            zip.AddEntry($"Barcode_{huNumber}.png", imageStream.ToArray());
                        }
                    }

                    // Save ZIP to stream
                    zip.Save(zipStream);
                    zipStream.Position = 0;

                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(zipStream.ToArray())
                    };

                    response.Content.Headers.ContentType =
                        new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");

                    response.Content.Headers.ContentDisposition =
                        new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                        {
                            FileName = $"Barcodes_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
                        };

                    return response;
                }
            }
            catch (Exception ex)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(ex.Message);

                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new ByteArrayContent(bytes)
                    {
                        Headers =
                        {
                            ContentType =
                                new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain"),
                            ContentDisposition =
                                new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                                {
                                    FileName = "error.txt"
                                }
                        }
                    }
                };
            }
        }
    }
}
