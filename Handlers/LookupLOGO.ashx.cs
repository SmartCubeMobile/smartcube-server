// Doesn't need encrypting (even I'M not going to encrypt a binary file!!)
// because the display of this symbol merely reflects the postcode area of the
// Consumer - you would have to be a IDIOT to imply that any personal
// details could be deduced from it...

using SmartCubeMobile;

namespace SmartCubeMobileV2026
{
    /// <summary>
    /// Summary description for LookupLOGO
    /// </summary>
    public class LookupLOGO
    {
        // Must have constructor with this signature, otherwise exception at run time
        public LookupLOGO(RequestDelegate next)
        {
            // This is an HTTP Handler, so no need to store next
        }
        public static async Task Invoke(HttpContext context)
        {
            //Debugger.Break();
            string target = "Images";

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
                // Oh! So we DO have a Referer .. this is a Decoy of course
                // The Handler knows the private key from SmartParametersV2016.privateKey
                // so it can decrypt the first 'Time Now' parameter (which is used to decrypt the others)
                char cubefaceCode = SmartParametersV2016.defaultChar;
                string random_key = "";
                string encrypted_time_now = string.Empty;
                string time_now = string.Empty;
                string encrypted_username = string.Empty;
                string username = string.Empty;
                string encrypted_brandDNO = string.Empty;
                string brandDNO = string.Empty;
                var xyz = context.Request.Form;
                foreach (var entry in xyz)
                {
                    switch (entry.Key)
                    {
                        case SmartParametersV2016.zeroParameter:
                            cubefaceCode = Convert.ToChar(entry.Value);
                            break;
                        case SmartParametersV2016.firstParameter:
                            random_key = entry.Value;
                            break;
                        case SmartParametersV2016.secondParameter:
                            encrypted_time_now = entry.Value;
                            time_now = SmartEncryptionV2016.DoTheBiz(string.Empty, random_key, encrypted_time_now, false);
                            break;
                        case SmartParametersV2016.thirdParameter:
                            // Username is encrypted with 'Time Now'
                            encrypted_username = entry.Value;
                            username = SmartEncryptionV2016.DoTheBiz(string.Empty, time_now, encrypted_username, false);
                            break;
                        case SmartParametersV2016.fourthParameter:
                            encrypted_brandDNO = entry.Value;
                            // DNO is decrypted with 'Time Now'
                            brandDNO = SmartEncryptionV2016.DoTheBiz(string.Empty, time_now, encrypted_brandDNO, false);
                            break;
                        default:
                            break;
                    }
                }

                context.Response.ContentType = SmartParametersV2016.contentImage;

                // See above !!
                string data_string = brandDNO + ".png";
                // Make the REAL target
                switch (cubefaceCode)
                {
                    case SmartParametersV2016.Finance:
                        target = Path.Combine(target, "Finance");
                        break;
                    case SmartParametersV2016.Utility:
                        target = Path.Combine(target, "Utility");
                        break;
                    default:
                        break;
                }
                target = Path.Combine(target, data_string);

                // I wasted a WHOLE DAY on this shit and I t-h-i-n-k this stuff works ..so               
                context.Response.Clear();                       // don't remove unless absolutely sure not needed
                context.Response.ContentType = SmartParametersV2016.contentImage;    // don't remove unless absolutely sure not needed
                if (!string.IsNullOrEmpty(target))
                {
                    try
                    {
                        // Can probably use this technique on the PDF downloads as well ...?
                        //
                        // Thanks to Jodrell http://stackoverflow.com/questions/18331349/c-sharp-4-5-file-read-performance-sync-vs-async
                        // the above two sync working lines were turned into this async solution:
                        //

                        // Thanks to Nina Ri
                        // https://forums.asp.net/t/2159112.aspx?HttpHandler+in+asp+net+core+Response+BinaryWrite+does+not+contain+a+definition
                        // but no thanks at all to those Microshit chimps
                        await Task.Run(() => context.Response.Body.WriteAsync(ReadFile(target)));    // Was BinaryWrite
                    }
                    catch (Exception)
                    {
                        // Just trap exceptions e.g. file not there or unreadable - don't do anything
                    }
                }
                // Well ... it doesn't exist in this Asp .Net Core bollocks so I'm going to cross my fingers
                // hold my breath and comment the fout.  Microshite chimps at it again - can't leave ANYTHING a - flone
                //context.Response.End();                         // don't remove until absolutely sure not needed
            }
        }

        static byte[] ReadFile(string target)
        {
            return File.ReadAllBytes(target);
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

    public static class LookupLOGOExtensions
    {
        public static IApplicationBuilder UseMyHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<Connect>();
        }
    }
}