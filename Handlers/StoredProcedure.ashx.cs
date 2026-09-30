// No need to encrypt - only ever called from SMARTCUBEMOBILE(?)

using SmartCubeMobile;
using System.IO.Pipes;
using System.Text;
using System.Diagnostics;

namespace SmartCubeMobileV2026
{
    /// <summary>
    /// Summary description for StoredProcedure
    /// </summary>
    public class StoredProcedure
    {
        // Must have constructor with this signature, otherwise exception at run time
        public StoredProcedure(RequestDelegate next)
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
                if (referer.IndexOf(SmartParametersV2016.permittedReferer) >= 0)    // Twat!
                {
                    string username = string.Empty;
                    var xyz = context.Request.Form;
                    foreach (var entry in xyz)
                    {
                        switch (entry.Key)
                        {
                            case SmartParametersV2016.usernameParameter:
                                username = entry.Value;
                                break;
                            default:
                                break;
                        }
                    }
                    //string username = context.Request.Params[SmartParametersV2016.username_parameter];
                    if (!string.IsNullOrEmpty(username))
                    {
                        context.Response.ContentType = SmartParametersV2016.contentType;

                        string operation = string.Empty;
                        string database_name = string.Empty;
                        string procedure_name = string.Empty;
                        string p1 = string.Empty;
                        foreach (var entry in xyz)
                        {
                            switch (entry.Key)
                            {
                                case SmartParametersV2016.operationParameter:
                                    operation = entry.Value;
                                    break;
                                case SmartParametersV2016.databaseParameter:
                                    database_name = entry.Value;
                                    break;
                                case SmartParametersV2016.procedureParameter:
                                    procedure_name = entry.Value;
                                    break;
                                case SmartParametersV2016.p1Parameter:
                                    p1 = entry.Value;
                                    break;
                                default:
                                    break;
                            }
                        }
                        string message_out = username + SmartParametersV2016.ourSeparator +            // field[0]
                                        operation + SmartParametersV2016.ourSeparator +           // field[1]
                                        database_name + SmartParametersV2016.ourSeparator +       // field[2]
                                        procedure_name;                                           // field[3]
                        if (!string.IsNullOrEmpty(p1))
                        {
                            message_out = message_out + SmartParametersV2016.ourSeparator + p1;
                        }

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

    public static class StoredProcedureExtensions
    {
        public static IApplicationBuilder UseMyHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<Connect>();
        }
    }
}