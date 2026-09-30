// CANDIDATE FOR ENCRYPTION (even though it just retrives SmartDBServer operations its our test)

using SmartCubeMobile;
using System.IO.Pipes;
using System.Text;
using System.Diagnostics;

namespace SmartCubeMobileV2026
{
    /// <summary>
    /// Summary description for DBServer
    /// </summary>
    public class DBServer
    {
        // Must have constructor with this signature, otherwise exception at run time
        public DBServer(RequestDelegate next)
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
                if (referer.IndexOf(SmartParametersV2016.permittedReferer) >= 0)    // !!! You twat!
                {
                    string username = string.Empty;
                    string operation = string.Empty;
                    string table_name = string.Empty;
                    string p1Parameter = string.Empty;
                    string secondParameter = string.Empty;
                    var xyz = context.Request.Form;
                    foreach (var entry in xyz)
                    {
                        switch (entry.Key)
                        {
                            case SmartParametersV2016.usernameParameter:
                                username = entry.Value;
                                break;
                            case SmartParametersV2016.operationParameter:
                                operation = entry.Value;
                                break;
                            case SmartParametersV2016.tableParameter:
                                table_name = entry.Value;
                                break;
                            case SmartParametersV2016.p1Parameter:
                                p1Parameter = entry.Value;
                                break;
                            case SmartParametersV2016.secondParameter:
                                secondParameter = entry.Value;
                                break;
                            default:
                                break;
                        }
                    }
                    // Check the parameters
                    if (!string.IsNullOrEmpty(username) &&
                        !string.IsNullOrEmpty(operation) &&
                        !string.IsNullOrEmpty(table_name))
                    {
                        //http://stackoverflow.com/questions/1003275/how-to-convert-byte-to-string
                        context.Response.ContentType = SmartParametersV2016.contentType;

                        string message_out = username + SmartParametersV2016.ourSeparator +
                                        operation + SmartParametersV2016.ourSeparator +
                                        table_name + SmartParametersV2016.ourSeparator +
                                        p1Parameter + SmartParametersV2016.ourSeparator +
                                        secondParameter;
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
                                await context.Response.WriteAsync(SmartParametersV2016.operationFailure.ToString());
                            }
                        }
                    }
                }
            }
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

    public static class DBServerExtensions
    {
        public static IApplicationBuilder UseMyHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<Connect>();
        }
    }   
}