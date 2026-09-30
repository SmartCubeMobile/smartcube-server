// This DOES do PDFs now because YOU, Ray, are a genius ... !!!
// Again, this isn't perfect but it should do for the time being
// If it ain't broken, don't fix it!

// Update 03-Oct-2015  - I spent over a week fixing this thing because it 'stopped working'
// I don't really know why, but I have re-arranged it to make it 'cookie character compatible' so it
// doesn't use cookie split and field split characters which are part of the official cookie character set
// Ti USE_PROXY for British Gas - you **ALWAYS** need the JSESSIONID and BG_COOKIE_ID cookies to be
// set and updated and read etc.  It just WON'T WORK without them.
// To test this, uncheck the Debugger.Break() lines and then build and run SmartCubeMobileV2026
// Unless you have done this, the Debugger.Break() breaks won't occur and you can't/won't see a thing.
// Also, don't forget if you CHANGE any part of this program, then SmartDBServer might need to be
// changed as well.
// Also, I check for the FIRST JSESSIONID and FIRST BG_COOKIE_ID cookie names and only use the first ones
// in previous versions of this program I used to get TWO JSESSIONID cookies and I always had to use the first
// Don't seem to get that now, but that is something I can check out in a years time when I have calmed down!

// %x21 / %x23-2B / %x2D-3A / %x3C-5B / %x5D-7E
// Which equals this:
// 0x21: !
// 0x23-2B: #$%&'()*+
// 0x2D-3A: -./0123456789: 
// 0x3C-5B: <=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[
// 0x5D - 7E: ]^_`abcdefghijklmnopqrstuvwxyz{|}~

// Specifically it states this:
// US-ASCII characters excluding CTLs,
// whitespace, DQUOTE, comma, semicolon,
// and backslash

using SmartCubeMobile;
using System.Collections;
using System.Reflection;
using System.Text;  // For Encode
using System.IO.Pipes;
using System.Net;

// THAT's IT - IT WORKS - NOW IF IT AIN'T BROKE, DON'T FIX IT => THIS MEANS YOU <== !!!!!
namespace SmartCubeMobileV2026
{
    /// <summary>
    /// Summary description for Webproxy
    /// </summary>
    public class Webproxy
    {
        public static CookieContainer cookieContainer;
        public static bool update;

        // Must have constructor with this signature, otherwise exception at run time
        public Webproxy(RequestDelegate next)
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
                if (referer.IndexOf(SmartParametersV2016.permittedDomain) >= 0)    // Twat!
                {
                    context.Response.ContentType = SmartParametersV2016.contentType;    // Default
                    string cookie_split = ";";   // i.e. a single ;
                    char field_split = SmartParametersV2016.unitSeparator;    // Which SHOULDN'T be in the cookie character set (I hope!)

                    // This was done in SPITE of that idiot, not BECAUSE of her ..
                    try
                    {
                        //Code snippet to get parameter value .
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
                            string table_name = "Cookie_Container";
                            string guid = string.Empty;
                            string time_now_yymmdd = string.Empty;
                            string method = string.Empty;
                            string mime = string.Empty;
                            string target_url = string.Empty;
                            string user_agent = string.Empty;
                            // Get all the remaining params
                            foreach (var entry in xyz)
                            {
                                switch (entry.Key)
                                {
                                    case SmartParametersV2016.guidParameter:
                                        guid = entry.Value;
                                        break;
                                    case SmartParametersV2016.timeNowParameter:
                                        // =====> This is different from Handler1  <=====
                                        time_now_yymmdd = entry.Value;
                                        break;
                                    case SmartParametersV2016.methodParameter:
                                        method = entry.Value;
                                        break;
                                    case SmartParametersV2016.mimeParameter:
                                        mime = entry.Value;
                                        break;
                                    case SmartParametersV2016.targetParameter:
                                        target_url = entry.Value;
                                        break;
                                    case SmartParametersV2016.userAgentParameter:
                                        user_agent = entry.Value;
                                        break;
                                    default:
                                        break;
                                }
                            }
                            // Is horizontal 'across' ??!!!!!!!!!!!!!!??!!!!!!!!!!!
                            //
                            // This stuff is SLOWLY KILLING ME
                            //               =================
                            //CookieContainer cookie_container = new CookieContainer();
                            cookieContainer = new CookieContainer();

                            //bool update = false;
                            update = false;
                            switch (method)
                            {
                                case "GET":     // This does PDF GETs as well as 'normal' GETs
                                    Uri uriGET = new Uri(Uri.UnescapeDataString(target_url));
                                    // Status is always good
                                    await GetCookies(username, SmartParametersV2016.serverName, SmartParametersV2016.pipename, SmartParametersV2016.serverTimeoutSecs, uriGET,
                                        //s => cookie_container = s, 
                                        table_name, guid, cookie_split);
                                    //, t => update = t);
                                    
                                    //
                                    // This HttpClient code replaces the HttpWebRequest
                                    // shit below, as this appears to be obsolete now.
                                    // It HASN'T EVER BEEN TESTED as I think all this
                                    // webproxy stuff is now redundant in any case
                                    // and I haven't got wither the time OR the inclination
                                    // to test or check possibly redundant code. So ther
                                    //
                                    // Thanks for nothing, Chimps
                                    //
                                    // Create the client
                                    HttpClientHandler get_handler = new HttpClientHandler()
                                    {
                                        CookieContainer = cookieContainer
                                    };
                                    HttpClient get_client = new HttpClient(get_handler)
                                    {
                                        //Timeout = timespanTimeout
                                    };
                                    System.Net.Http.Headers.MediaTypeWithQualityHeaderValue accept = 
                                        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/html, application/xhtml+xml, */*");
                                    get_client.DefaultRequestHeaders.Accept.Add(accept);
                                    if (!string.IsNullOrEmpty(user_agent))
                                    {
                                        System.Net.Http.Headers.ProductInfoHeaderValue useragent =
                                        new System.Net.Http.Headers.ProductInfoHeaderValue(user_agent);
                                        get_client.DefaultRequestHeaders.UserAgent.Add(useragent);
                                    }
                                    try     // The network might have gone away
                                    {
                                        HttpResponseMessage get_response = await get_client.GetAsync(uriGET);
                                        if (get_response.IsSuccessStatusCode)
                                        {
                                            // For PDFs?
                                            if (mime != "PDF")
                                            {
                                                context.Response.Body = await get_response.Content.ReadAsStreamAsync(); // OutputStream
                                            }
                                            else
                                            {
                                                context.Response.ContentType = "application/octet-stream"; 
                                                context.Response.Body = await get_response.Content.ReadAsStreamAsync(); // OutputStream
                                            }
                                        }
                                    }
                                    
                            
                                    // This is the obsolete stuff replaced
                                    // HttpWebRequest get_client = HttpWebRequest.CreateHttp(uriGET);
                                    // get_client.CookieContainer = cookieContainer;
                                    // get_client.Accept = "text/html, application/xhtml+xml, */*";
                                    // if (!string.IsNullOrEmpty(user_agent))
                                    // {
                                    //    // Just for EDF!!!
                                    //    get_client.UserAgent = user_agent;
                                    // }
                                    // try      // The network may have gone away!
                                    // {
                                    //    HttpWebResponse get_response = (HttpWebResponse)await get_client.GetResponseAsync();
                                    //    // For PDFs?
                                    //    if (mime != "PDF")
                                    //    {
                                    //        get_response.GetResponseStream().CopyTo(context.Response.Body);// OutputStream);
                                    //    }
                                    //    else
                                    //    {
                                    //        context.Response.ContentType = "application/octet-stream";
                                    //        get_response.GetResponseStream().CopyTo(context.Response.Body); // OutputStream);
                                    //    }
                                    //    await PutCookies(username, SmartParametersV2016.serverName, SmartParametersV2016.pipename, SmartParametersV2016.serverTimeoutSecs, get_client.CookieContainer, table_name, guid, time_now_yymmdd, cookie_split, field_split, update);
                                    // }
                                    catch (Exception)
                                    {
                                        await context.Response.WriteAsync(SmartParametersV2016.operationUnsure.ToString());
                                    }
                                    break;
                                case "POST":
                                    string postdata = string.Empty;
                                    foreach (var entry in xyz)
                                    {
                                        switch (entry.Key)
                                        {
                                            case "Postdata":
                                                postdata = entry.Value;
                                                break;
                                            default:
                                                break;
                                        }
                                    }
                                    //string postdata = context.Request.Params["Postdata"].ToString();
                                    postdata = Uri.UnescapeDataString(postdata);

                                    Uri uriPOST = new Uri(Uri.UnescapeDataString(target_url));
                                    // Status is always good
                                    await GetCookies(username, SmartParametersV2016.serverName, SmartParametersV2016.pipename, SmartParametersV2016.serverTimeoutSecs, uriPOST,
                                        //s => cookie_container = s, 
                                        table_name, guid, cookie_split);
                                    //, t => update = t);

                                    //
                                    // This HttpClient code replaces the HttpWebRequest
                                    // shit below, as this appears to be obsolete now.
                                    // It HASN'T EVER BEEN TESTED as I think all this
                                    // webproxy stuff is now redundant in any case
                                    // and I haven't got wither the time OR the inclination
                                    // to test or check possibly redundant code. So ther
                                    //
                                    // Thanks for nothing, Chimps
                                    //
                                    // Create the client
                                    HttpClientHandler post_handler = new HttpClientHandler()
                                    {
                                        CookieContainer = cookieContainer
                                    };
                                    HttpClient post_client = new HttpClient(post_handler)
                                    {
                                        //Timeout = timespanTimeout
                                    };
                                    
                                    System.Net.Http.Headers.MediaTypeWithQualityHeaderValue post_accept =
                                        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/html, application/xhtml+xml, */*");
                                    post_client.DefaultRequestHeaders.Accept.Add(post_accept);
                                    HttpContent post_content = new FormUrlEncodedContent(new[]
                                    {
                                            new KeyValuePair<string, string>("", postdata)
                                    });
                                    try     // The network might have gone away
                                    {
                                        HttpResponseMessage post_response = await post_client.PostAsync(uriPOST, post_content);                                        
                                        if (post_response.IsSuccessStatusCode)
                                        {
                                            Encoding encode = System.Text.Encoding.GetEncoding("utf-8");
                                            

                                            StreamReader post_stream = new StreamReader(await post_response.Content.ReadAsStreamAsync(), encode);
                                            
                                                                                      
                                            await context.Response.WriteAsync(await post_stream.ReadToEndAsync());
                                            await PutCookies(username, SmartParametersV2016.serverName, SmartParametersV2016.pipename, SmartParametersV2016.serverTimeoutSecs, post_handler.CookieContainer, table_name, guid, time_now_yymmdd, cookie_split, field_split, update);
                                        }
                                    }

                                    // HttpWebRequest post_client = HttpWebRequest.CreateHttp(uriPOST);
                                    // post_client.Method = "POST";
                                    // post_client.CookieContainer = cookieContainer;

                                    // post_client.ContentType = "application/x-www-form-urlencoded";
                                    // post_client.Accept = "text/html, application/xhtml+xml, */*";
                                    // if (!string.IsNullOrEmpty(user_agent))
                                    // {
                                    //    // Just for EDF!!! (Hope it works on a POST??)
                                    //    post_client.UserAgent = user_agent;
                                    // }
                                    // post_client.ContentLength = postdata.Length;
                                    // Write the content to be POSTed
                                    // StreamWritr request = new StreamWritr(await post_client.GetRequestStreamAsync());
                                    // await request.WriteAsync(postdata);
                                    // request.Close();
                                    // try    // The network may have gone away
                                    // {
                                    //    HttpWebResponse post_response = (HttpWebResponse)await post_client.GetResponseAsync();
                                    //    // For PDFs?
                                    //    Encoding encode = System.Text.Encoding.GetEncoding("utf-8");
                                    //    // Maybe one day we can make this a 'CopyTo' and include the encodeing?
                                    //    await PutCookies(username, SmartParametersV2016.serverName, SmartParametersV2016.pipename, SmartParametersV2016.serverTimeoutSecs, post_client.CookieContainer, table_name, guid, time_now_yymmdd, cookie_split, field_split, update);
                                    //}
                                    catch (Exception)
                                    {
                                        await context.Response.WriteAsync(SmartParametersV2016.operationUnsure.ToString());
                                    }
                                    break;
                                case "DELETE":
                                    await DeleteCookies(username, SmartParametersV2016.serverName, SmartParametersV2016.pipename, SmartParametersV2016.serverTimeoutSecs, table_name, guid);
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        await context.Response.WriteAsync(SmartParametersV2016.operationUnsure.ToString());
                    }
                }
            }
        }

        public static async Task<char> DeleteCookies(string username,
                                                string servername,
                                                string pipename,
                                                int servertimeoutms,
                                                string tablename,
                                                string ourguid)
        {
            // Assume failure first
            char status = SmartParametersV2016.operationFailure;

            using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(servername,
                                                                                pipename,
                                                                                PipeDirection.InOut,
                                                                                PipeOptions.Asynchronous))
            {
                try
                {
                    // The connect function will indefinitely wait for the pipe to become available

                    // If that is not acceptable specify a maximum waiting time (in ms)
                    await pipeClient.ConnectAsync(servertimeoutms);
                    
                    string sql = "DELETE FROM " + tablename +
                                    " WHERE USERNAME='" + username + "'" +
                                    " AND GUID='" + ourguid + "'";
                    sql = username + SmartParametersV2016.ourSeparator + tablename + SmartParametersV2016.ourSeparator + "D" + SmartParametersV2016.ourSeparator + sql;
                    
                    byte[] data = Encoding.UTF8.GetBytes(sql);

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
                    status = Convert.ToChar(response.Substring(0, 1));
                }
                catch (TimeoutException)
                {
                    // Failed to connect - return operationFailure
                }
            }
            // Status might be empty or X or tick here
            return status;
        }

        public static async Task<char> GetCookies(string username,
                                                    string servername,
                                                    string pipename,
                                                    int servertimeoutms,
                                                    Uri uri,
                                                    //CookieContainer setcookiecontainer,
                                                    string tablename,
                                                    string ourguid,
                                                    string cookiesplit) //,
                                                                        //bool setupdate)
        {
            // Assume failure first
            char status = SmartParametersV2016.operationFailure;

            using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(servername,
                                                                                pipename,
                                                                                PipeDirection.InOut,
                                                                                PipeOptions.Asynchronous))
            {
                try
                {
                    // The connect function will indefinitely wait for the pipe to become available
                    // If that is not acceptable specify a maximum waiting time (in ms)
                    await pipeClient.ConnectAsync(servertimeoutms);
                    
                    string sql = "SELECT COOKIES FROM " + tablename +
                                " WHERE USERNAME='" + username + "'" +
                                " AND GUID='" + ourguid + "'";
                    sql = username + SmartParametersV2016.ourSeparator +
                                    tablename + SmartParametersV2016.ourSeparator +
                                    "S" + SmartParametersV2016.ourSeparator +
                                    sql;

                    byte[] data = Encoding.UTF8.GetBytes(sql);

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

                    string retrieve_cookies = sb.ToString();
                    //await context.Response.WriteAsync(response);
                    //string retrieve_cookies = await stream_reader.ReadToEndAsync();
                    
                    retrieve_cookies = retrieve_cookies.Replace(Environment.NewLine, string.Empty);

                    
                    
                    //Debugger.Break();     // <= Uncheck this to debug then 'Run SmartCubeMobileV2026'
                    if (!string.IsNullOrEmpty(retrieve_cookies))
                    {
                        //Convert back to collection
                        CookieCollection cookie_collection = new CookieCollection();
                        string[] cookies = retrieve_cookies.Split(Convert.ToChar(cookiesplit)); // See above ; is not an allowed cookie character
                        foreach (string cookie_string in cookies)
                        {
                            string[] cookies_array = cookie_string.Split('=');  // See above = is not an allowed cookie character
                            Cookie cookie = new Cookie();
                            int item_count = 0;
                            foreach (string item in cookies_array)
                            {
                                switch (item_count)
                                {
                                    case 0:     // Name 0
                                        cookie.Name = item;
                                        break;
                                    case 1:     // Value 1
                                        cookie.Value = item;
                                        break;
                                    case 2:     // Domain 2
                                        cookie.Domain = item;
                                        break;
                                    case 3:     // Http 3
                                        cookie.HttpOnly = (item == "True" ? true : false);
                                        break;
                                    case 4:     // Path 4
                                        cookie.Path = item;
                                        break;
                                    case 5:     // Secure 5
                                        cookie.Secure = (item == "True" ? true : false);
                                        break;
                                }
                                item_count = item_count + 1;
                            }
                            cookie_collection.Add(cookie);
                        }
                        CookieContainer local = new CookieContainer();
                        local.Add(uri, cookie_collection);
                        //setcookiecontainer(local);
                        cookieContainer = local;
                        //setupdate(true);
                        update = true;
                        status = SmartParametersV2016.operationSuccess;
                    }
                }
                catch (TimeoutException)
                {
                    // Failed to connect - return operationFailure
                }
            }
            // All that is returned is a string of cookies (which may be empty)
            return status;
        }

        public static async Task<char> PutCookies(string username,
                                                    string servername,
                                                    string pipename,
                                                    int servertimeoutms,
                                                    CookieContainer cookiecontainer,
                                                    string tablename,
                                                    string ourguid,
                                                    string timenowyymmdd,
                                                    string cookiesplit,
                                                    char fieldsplit,
                                                    bool update)
        {
            //Debugger.Break();     // <= Uncheck this to debug then 'Run SmartCubeMobileV2026'
            // Assume failure first
            char status = SmartParametersV2016.operationFailure;

            string store_cookies = string.Empty;
            bool done_jsessionid = false,
                    done_bg_cookie_id = false;

            // Couldn't have done this without ANDRE ANDERSEN's code below
            // http://stackoverflow.com/questions/15983166/how-can-i-get-all-cookies-of-a-cookiecontainer
            Hashtable k = (Hashtable)cookiecontainer.GetType().GetField("m_domainTable", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cookiecontainer);
            foreach (DictionaryEntry element in k)
            {
                SortedList sorted_list = (SortedList)element.Value.GetType().GetField("m_list", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(element.Value);
                foreach (var list_element in sorted_list)
                {
                    CookieCollection cookie_collection = (CookieCollection)((DictionaryEntry)list_element).Value;
                    foreach (Cookie cookie in cookie_collection)
                    {
                        switch (cookie.Name)
                        {
                            case "JSESSIONID":
                                if (done_jsessionid)
                                {
                                    continue;
                                }
                                done_jsessionid = true;
                                break;
                            case "BG_COOKIE_ID":
                                if (done_bg_cookie_id)
                                {
                                    continue;
                                }
                                done_bg_cookie_id = true;
                                break;
                            default:
                                break;
                        }

                        if (!string.IsNullOrEmpty(store_cookies))
                        {
                            store_cookies = store_cookies + cookiesplit;    // See above ';' is not an allowed cookie character
                        }
                        store_cookies = store_cookies + cookie +
                                        "=" + cookie.Domain +
                                        "=" + cookie.HttpOnly +
                                        "=" + cookie.Path +
                                        "=" + cookie.Secure;
                    }
                }
            }

            // Now she's yapping again
            string values = ourguid + fieldsplit + timenowyymmdd + fieldsplit + store_cookies;

            using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(servername,
                                                                                pipename,
                                                                                PipeDirection.InOut,
                                                                                PipeOptions.Asynchronous))
            {
                try
                {
                    // The connect function will indefinitely wait for the pipe to become available
                    // If that is not acceptable specify a maximum waiting time (in ms)
                    await pipeClient.ConnectAsync(servertimeoutms);

                    StreamWriter stream_writer = new StreamWriter(pipeClient)
                    {
                        AutoFlush = true      // <= Very important or nothing works
                    };

                    string sql = string.Empty;
                    if (update)
                    {
                        // Username as a key is implied
                        // The "1" is just symbolic - it means 'no new record' i.e. we expect it to be there
                        // Look at the update_consumer_energy call in DBServer_V13 ... it just makes
                        // the call vaguely compatible
                        // We ONLY ever send one row - no need to split it at the other end
                        sql = username + SmartParametersV2016.ourSeparator + tablename + SmartParametersV2016.ourSeparator + "U" + SmartParametersV2016.ourSeparator + "1" + SmartParametersV2016.ourSeparator + values;
                    }
                    else
                    {
                        // We ONLY ever send one row - no need to split it at the other end
                        sql = username + SmartParametersV2016.ourSeparator + tablename + SmartParametersV2016.ourSeparator + "I" + SmartParametersV2016.ourSeparator + values;
                    }
                    //await stream_writer.WriteAsync(sql);

                    byte[] data = Encoding.UTF8.GetBytes(sql);

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
                    status = Convert.ToChar(response.Substring(0, 1));
                }
                catch (TimeoutException)
                {
                    // Failed to connect return operationFailure
                }
            }
            // Status might be empty or 'X' or 'tick' here
            return status;
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

    public static class WebproxyExtensions
    {
        public static IApplicationBuilder UseMyHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<Connect>();
        }
    }
}