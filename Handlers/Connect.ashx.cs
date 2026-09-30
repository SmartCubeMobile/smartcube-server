// ***ALL THE FOLLOWING IS OLD-HAT BOLLOCKS***
// The way the security works is this:
// When http:/localhost/SmartCubeMobile/Connect.ashx?Username=BOB is invoked
// from a browser such as IE directly, then the "Referer" component does not come through
// and so we never pick up the Referer component.  IN THIS CASE we don't contact SmartDBServer
// and return absolutely NOTHING in response

// This is the RELEVANT STUFF
// Its a candidate for encryption because we don't want spotty-faced twats
// seeing how we connect - even though we just send the Username in upper or lower case)

using SmartCubeMobile;
using System.IO.Pipes;
using System.Text;
using System.Diagnostics;

namespace SmartCubeMobileV2026
{
    /// <summary>
    /// Summary description for Connect
    /// </summary>
    public class Connect
    {
        // Must have constructor with this signature, otherwise exception at run time
        public Connect(RequestDelegate next)
        {
            // This is an HTTP Handler, so no need to store next
        }

        public static async Task Invoke(HttpContext context)
        {
            //Debugger.Break();
           
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
                string random_key = "";
                string encrypted_time_now;
                string time_now = string.Empty;
                string encrypted_username;
                string username = string.Empty;
                string encrypted_operation;
                string operation = string.Empty;
                string encrypted_groups; // Encrypted
                string groups = string.Empty;
                string utcdates = string.Empty;        // Not encrypted
                string exchangerates_lastdate = string.Empty;         // Not encrypted
                
                var xyz = context.Request.Form;
                foreach (var entry in xyz)
                {
                    switch (entry.Key)
                    {
                        case SmartParametersV2016.zeroParameter:
                            random_key = entry.Value;
                            break;
                        case SmartParametersV2016.firstParameter:
                            encrypted_time_now = entry.Value;
                            time_now = SmartEncryptionV2016.DoTheBiz(string.Empty, random_key, encrypted_time_now, false);
                            break;
                        case SmartParametersV2016.secondParameter:
                            // Username is encrypted with 'Time Now'
                            encrypted_username = entry.Value;
                            username = SmartEncryptionV2016.DoTheBiz(string.Empty, time_now, encrypted_username, false);
                            break;
                        case SmartParametersV2016.thirdParameter:
                            encrypted_operation = entry.Value;
                            operation = SmartEncryptionV2016.DoTheBiz(string.Empty, time_now, encrypted_operation, false);
                            break;
                        case SmartParametersV2016.fourthParameter:
                            encrypted_groups = entry.Value;
                            groups = SmartEncryptionV2016.DoTheBiz(string.Empty, time_now, encrypted_groups, false);
                            break;
                        case SmartParametersV2016.fifthParameter:
                            utcdates = entry.Value;
                            break;
                        case SmartParametersV2016.sixthParameter:
                            exchangerates_lastdate = entry.Value;
                            //lastdate = SmartEncryptionV2016.DoTheBiz(string.Empty, time_now, encrypted_lastdate, false);
                            break;
                        default:
                            break;
                    }
                }

                //
                // **ONLY** ever send a Connect message if we know who it is!
                //
                // However - you MIGHT want to change DBServer so it looks up ALL the valid usernames
                // and can check that they actually DO exist!
                //
                if (!string.IsNullOrEmpty(username))
                {
                    context.Response.ContentType = SmartParametersV2016.contentType;
                    using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(SmartParametersV2016.serverName,
                                                                                    SmartParametersV2016.pipename,
                                                                                    PipeDirection.InOut,
                                                                                    PipeOptions.Asynchronous))
                    {
                        try
                        {
                            // The connect function will indefinitely wait for the pipe to become available
                            // If that is not acceptable specify a maximum waiting time (in ms)
                            await pipeClient.ConnectAsync(SmartParametersV2016.serverTimeoutSecs);
                            pipeClient.ReadMode = PipeTransmissionMode.Message;

                            string message_out = username + SmartParametersV2016.ourSeparator +
                                                operation + SmartParametersV2016.ourSeparator +
                                                groups;
                            if (utcdates != "")
                            {
                                message_out += SmartParametersV2016.ourSeparator + utcdates;
                                if (exchangerates_lastdate != "")
                                {
                                    message_out += SmartParametersV2016.ourSeparator + exchangerates_lastdate;                                    
                                }
                            }

                            byte[] data = Encoding.UTF8.GetBytes(message_out);
                            await pipeClient.WriteAsync(data, 0, data.Length);
                            await pipeClient.FlushAsync();

                            var buffer = new byte[4096];
                            var sb = new StringBuilder();

                            do
                            {
                                int bytesRead = await pipeClient.ReadAsync(buffer, 0, buffer.Length);
                                if (bytesRead == 0)
                                    break;
                                sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                            }
                            while (!pipeClient.IsMessageComplete);

                            string response = sb.ToString();
                            await context.Response.WriteAsync(response);
                        }
                        catch (TimeoutException)
                        {
                            // Failed to connect
                            await context.Response.WriteAsync(SmartParametersV2016.operationFailure.ToString()); // Don't encrypt return codes
                        }
                    }
                }
            }
        }

        // ...

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

    public static class ConnectExtensions
    {
        public static IApplicationBuilder UseMyHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<Connect>();
        }
    }
}