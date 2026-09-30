// Encrypted parameters so twats can't see how to call it

using SmartCubeMobile;
using System.IO.Pipes;
using System.Text;
using System.Diagnostics;

namespace SmartCubeMobileV2026
{
    /// <summary>
    /// Summary description for LoadTable
    /// </summary>
    public class LoadTable
    {
        // Must have constructor with this signature, otherwise exception at run time
        public LoadTable(RequestDelegate next)
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
                string encrypted_time_now;// = string.Empty;
                string time_now = string.Empty;
                string encrypted_username;// = string.Empty;
                string username = string.Empty;
                string encrypted_operation;// = string.Empty;
                string operation = string.Empty;
                string encrypted_database_name;// = string.Empty;
                string database_name = string.Empty;
                string encrypted_table_name;// = string.Empty;
                string table_name = string.Empty;
                string encrypted_sql;// = string.Empty;
                string sql = string.Empty;
                string encrypted_schemas = string.Empty;
                string schemas = string.Empty;
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
                            // Data is decrypted with 'Time Now'
                            encrypted_operation = entry.Value;
                            operation = SmartEncryptionV2016.DoTheBiz(string.Empty, time_now, encrypted_operation, false);
                            break;
                        case SmartParametersV2016.fourthParameter:
                            // Data is decrypted with 'Time Now'
                            encrypted_database_name = entry.Value;
                            database_name = SmartEncryptionV2016.DoTheBiz(string.Empty, time_now, encrypted_database_name, false);
                            break;
                        case SmartParametersV2016.fifthParameter:
                            // Data is decrypted with 'Time Now'
                            encrypted_table_name = entry.Value;
                            table_name = SmartEncryptionV2016.DoTheBiz(string.Empty, time_now, encrypted_table_name, false);
                            break;
                        case SmartParametersV2016.sixthParameter:
                            // Data is decrypted with 'Time Now'
                            encrypted_sql = entry.Value;
                            sql = SmartEncryptionV2016.DoTheBiz(string.Empty, time_now, encrypted_sql, false);
                            break;
                        case SmartParametersV2016.seventhParameter:
                            // Data is decrypted with 'Time Now'
                            encrypted_schemas = entry.Value;
                            schemas = SmartEncryptionV2016.DoTheBiz(string.Empty, time_now, encrypted_schemas, false);
                            break;
                        default:
                            break;
                    }
                }

                // Check the decrypted parameters
                if (!string.IsNullOrEmpty(time_now) &&
                    !string.IsNullOrEmpty(username) &&
                    !string.IsNullOrEmpty(operation) &&
                    !string.IsNullOrEmpty(database_name) &&
                    !string.IsNullOrEmpty(table_name))
                {
                    context.Response.ContentType = SmartParametersV2016.contentType;

                    string message_out = username + SmartParametersV2016.ourSeparator +            // field[0]
                                    operation + SmartParametersV2016.ourSeparator +           // field[1]
                                    database_name + SmartParametersV2016.ourSeparator +       // field[2]
                                    table_name;                                               // field[3]
                    if (!string.IsNullOrEmpty(sql))
                    {
                        message_out = message_out + SmartParametersV2016.ourSeparator + sql;            // field[4]
                        if (!string.IsNullOrEmpty(schemas))
                        {
                            message_out = message_out + SmartParametersV2016.ourSeparator + schemas;            // field[4]
                        }
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
                            await context.Response.WriteAsync(SmartParametersV2016.operationFailure.ToString());// Don't encrypt return codes
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

    public static class LoadTableExtensions
    {
        public static IApplicationBuilder UseMyHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<Connect>();
        }
    }
}