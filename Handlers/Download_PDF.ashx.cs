// Need to enctypt the PDF before download ...
// I am going to attempt this because I have BALLS OF STEEL - but not before I've thought about it first!

using SmartCubeMobile;

namespace SmartCubeMobileV2026
{
    /// <summary>
    /// Summary description for Download_PDF
    /// </summary>
    public class Download_PDF
    {
        // Must have constructor with this signature, otherwise exception at run time
        public Download_PDF(RequestDelegate next)
        {
            // This is an HTTP Handler, so no need to store next
        }
        public static async Task Invoke(HttpContext context)
        {
            //Debugger.Break();
            string error_message = string.Empty;

            string referer = string.Empty;
            var abc = context.Request.Headers;
            foreach (var entry in abc)
            {
                if (entry.Key == SmartParametersV2016.refererKey)
                {
                    referer = entry.Value;
                    break;
                }
            }
            if (!string.IsNullOrEmpty(referer))
            {
                context.Response.ContentType = SmartParametersV2016.contentType;

                // This generic handler deals in its entirety with storing
                // PDF files which have failed to be parsed correctly.
                // There is no need to pass the contents on to DBServer
                // because a) the DBServer named pipe will *not* tell
                // me how many bytes have been sent so I can't accurately
                // store them and write them out and b) the named pipe appears
                // to be designed for strings and not binary data.
                // Its a complete cock-up on Microshit's part.  They are nigh
                // on fuseless...

                long len = context.Request.Body.Length; // ?? InputStream.Length;
                string time_now = string.Empty;
                int terminator_pos = 0;
                using (BinaryReader inputStream = new BinaryReader(context.Request.Body)) // ?? InputStream))
                {
                    if (OurSeparatorSearch(string.Empty, len, ref terminator_pos, ref time_now, inputStream))
                    {
                        string username = string.Empty;
                        if (OurSeparatorSearch(string.Empty, len, ref terminator_pos, ref username, inputStream))
                        {
                            string resource = string.Empty;
                            if (OurSeparatorSearch(string.Empty, len, ref terminator_pos, ref resource, inputStream))
                            {
                                string target = System.IO.Path.Combine(SmartParametersV2016.pdfDownloadPath, username);

                                string filename = string.Empty;
                                if (OurSeparatorSearch(resource + "_", len, ref terminator_pos, ref filename, inputStream))
                                {
                                    if (!File.Exists(target))
                                    {
                                        // Create a file to write to. 
                                        Directory.CreateDirectory(target);
                                    }

                                    target = Path.Combine(target, filename);
                                    using (BinaryWriter outputFile = new BinaryWriter(File.Open(target, FileMode.Create)))
                                    {
                                        len = len - terminator_pos;
                                        try
                                        {
                                            await Task.Run(() => outputFile.Write(inputStream.ReadBytes((int)len), 0, (int)len));
                                        }
                                        catch (Exception)
                                        {
                                            // Failed to receive - or store - or whatever - something went wrong
                                            await context.Response.WriteAsync(SmartParametersV2016.operationFailure.ToString());
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                // Everything worked ... say we are good
                await context.Response.WriteAsync(SmartParametersV2016.operationSuccess.ToString());
            }
        }

        public static bool OurSeparatorSearch(string initialvalue, long len, ref int terminatorpos, ref string parameter, BinaryReader inputStream)
        {
            parameter = initialvalue;
            bool ourSeparator_found = false;
            while (terminatorpos < len)
            {
                byte rays = inputStream.ReadByte();
                terminatorpos = terminatorpos + 1;
                if (rays == Convert.ToByte(SmartParametersV2016.ourSeparator))
                {
                    ourSeparator_found = true;
                    break;
                }
                parameter = parameter + Convert.ToString(Convert.ToChar(rays));
            }
            return ourSeparator_found;
        }

        private string GenerateResponse(HttpContext context)
        {
            string title = context.Request.Query["title"];
            return string.Format("Title of the report: {0}", title);
        }

        private string GetContentType()
        {
            return "text/plain";
        }
    }

    public static class Download_PDFExtensions
    {
        public static IApplicationBuilder UseMyHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<Connect>();
        }
    }
}