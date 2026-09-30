// Smart Scan proxy: validates a licence key, forwards OCR text to Claude, returns extracted JSON.
// The Claude API key lives in appsettings.json (Claude:ApiKey) and never leaves this server.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace SmartCubeMobileV2026
{
    public class SmartScan
    {
        private const int MaxInputChars = 60000;
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(90) };
        private static string _apiKey;
        private static string _model;
        private static string _connectionString;

        public SmartScan(RequestDelegate next) { }

        public static void Configure(IConfiguration config)
        {
            _apiKey = config["Claude:ApiKey"];
            _model = config["Claude:Model"] ?? "claude-haiku-4-5-20251001";
            _connectionString = config.GetConnectionString("DefaultConnection");
        }

        public static async Task Invoke(HttpContext context)
        {
            context.Response.ContentType = "application/json";

            if (!HttpMethods.IsPost(context.Request.Method))
            {
                await Fail(context, 405, "method_not_allowed", "POST only");
                return;
            }

            var form = await context.Request.ReadFormAsync();
            string licence = (form["licence"].ToString() ?? "").Trim().ToUpperInvariant();
            string category = (form["category"].ToString() ?? "").Trim();
            string text = form["text"].ToString() ?? "";

            if (string.IsNullOrEmpty(licence))
            {
                await Fail(context, 401, "invalid_licence", "Licence key required");
                return;
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                await Fail(context, 400, "no_text", "No document text supplied");
                return;
            }
            if (text.Length > MaxInputChars)
                text = text[..MaxInputChars];

            var (valid, code, message) = await CheckLicence(licence);
            if (!valid)
            {
                await Fail(context, 403, code, message);
                return;
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                await Log(licence, category, text.Length, false, "Server has no Claude API key configured");
                await Fail(context, 200, "server_config", "Smart Scan is not configured on the server");
                return;
            }

            try
            {
                var extracted = await CallClaude(text, category);
                await ConsumeScan(licence);
                await Log(licence, category, text.Length, true, null);

                var body = new JsonObject { ["ok"] = true, ["data"] = extracted };
                await context.Response.WriteAsync(body.ToJsonString());
            }
            catch (Exception ex)
            {
                await Log(licence, category, text.Length, false, ex.Message);
                // 200 not 502: Cloudflare swallows origin 502s and returns its own page, hiding the JSON
                await Fail(context, 200, "api_error", ex.Message);
            }
        }

        private static async Task<(bool Valid, string Code, string Message)> CheckLicence(string licence)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT IsActive, ScansUsed, ScanLimit, Expires FROM dbo.SmartScanLicences WHERE LicenceKey = @k", conn);
            cmd.Parameters.AddWithValue("@k", licence);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return (false, "invalid_licence", "Licence key not recognised");

            bool isActive = reader.GetBoolean(0);
            int used = reader.GetInt32(1);
            int limit = reader.GetInt32(2);
            DateTime? expires = reader.IsDBNull(3) ? null : reader.GetDateTime(3);

            if (!isActive)
                return (false, "licence_inactive", "Licence has been deactivated");
            if (expires.HasValue && expires.Value < DateTime.UtcNow)
                return (false, "licence_expired", "Licence has expired");
            if (used >= limit)
                return (false, "limit_reached", $"Scan limit of {limit} reached");

            return (true, null, null);
        }

        private static async Task ConsumeScan(string licence)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "UPDATE dbo.SmartScanLicences SET ScansUsed = ScansUsed + 1, LastUsed = SYSUTCDATETIME() WHERE LicenceKey = @k", conn);
            cmd.Parameters.AddWithValue("@k", licence);
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task Log(string licence, string category, int chars, bool success, string error)
        {
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(
                    "INSERT INTO dbo.SmartScanLog (LicenceKey, Category, InputChars, Success, Error) VALUES (@k, @c, @n, @s, @e)", conn);
                cmd.Parameters.AddWithValue("@k", licence);
                cmd.Parameters.AddWithValue("@c", (object)category ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@n", chars);
                cmd.Parameters.AddWithValue("@s", success);
                cmd.Parameters.AddWithValue("@e", (object)(error?.Length > 400 ? error[..400] : error) ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            catch { }
        }

        private static async Task<JsonObject> CallClaude(string ocrText, string category)
        {
            var prompt = $@"You are extracting insurance policy details from OCR text. The document is a {category} insurance policy.

Extract the following fields from the text below. Return ONLY a JSON object with these fields (use null for any field you cannot find):

{{
  ""provider"": ""insurance company name"",
  ""policyNumber"": ""policy/reference number"",
  ""policyType"": ""e.g. Comprehensive, Third Party, Buildings & Contents, Term Life, Lifetime"",
  ""annualPremium"": 0.00,
  ""monthlyPremium"": 0.00,
  ""excess"": 0.00,
  ""coverAmount"": 0.00,
  ""startDate"": ""DD/MM/YYYY"",
  ""endDate"": ""DD/MM/YYYY"",
  ""namedInsured"": ""policyholder full name"",
  ""vehicleReg"": ""registration number (car only)"",
  ""vehicleMakeModel"": ""make and model (car only)"",
  ""propertyAddress"": ""insured property address (house only)"",
  ""petName"": ""pet name (pet only)"",
  ""petBreed"": ""breed (pet only)""
}}

Rules:
- For annualPremium, use the total amount paid per year including all fees
- If only annual is stated, calculate monthly as annual / 12
- If only monthly is stated, calculate annual as monthly * 12
- For excess, use the total compulsory + voluntary excess for the most common claim type
- Dates must be DD/MM/YYYY format
- For vehicleMakeModel include the full make and model e.g. ""Mercedes-Benz C 200K Classic""
- Return ONLY the JSON object, no other text

OCR Text:
{ocrText}";

            var requestBody = new JsonObject
            {
                ["model"] = _model,
                ["max_tokens"] = 1024,
                ["messages"] = new JsonArray
                {
                    new JsonObject { ["role"] = "user", ["content"] = prompt }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            using var response = await _http.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                string apiMsg = null;
                try { apiMsg = JsonNode.Parse(responseBody)?["error"]?["message"]?.GetValue<string>(); } catch { }
                throw new Exception(apiMsg ?? $"Claude API error {(int)response.StatusCode}");
            }

            var content = JsonNode.Parse(responseBody)?["content"]?[0]?["text"]?.GetValue<string>();
            if (string.IsNullOrEmpty(content))
                throw new Exception("Empty response from Claude");

            var jsonMatch = Regex.Match(content, @"\{[\s\S]*\}");
            if (!jsonMatch.Success)
                throw new Exception("No JSON in Claude response");

            return JsonNode.Parse(jsonMatch.Value) as JsonObject
                   ?? throw new Exception("Claude response was not a JSON object");
        }

        private static async Task Fail(HttpContext context, int status, string code, string message)
        {
            context.Response.StatusCode = status;
            var body = new JsonObject { ["ok"] = false, ["code"] = code, ["error"] = message };
            await context.Response.WriteAsync(body.ToJsonString());
        }
    }
}
