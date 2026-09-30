using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using SmartCubeMobileV2026.Areas.Identity.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using SmartCubeMobile;

namespace SmartCubeMobileV2026.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        public static string RaysText { get; set; }        // ? To stop this drivel failing
        public const string BrandLogoPath = "/images/smartcube-mobile-logo.png";
        public static class Aaargh
        {
            public static string ChallengeTime { get; set; }   // ? To make this utter bollocks works
        }

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(SignInManager<ApplicationUser> signInManager, 
            ILogger<LoginModel> logger,
            UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [DataType(DataType.Text)]
            public string UserName { get; set; }   // ? Otherwise this bollocks doesn't work

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }   // ? Otherwise this crap crashes on you

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }

            public string Raymondo { get; set; }   // ? Otherise this shit dumps on you
        }       
        
        public async Task OnGetAsync(string returnUrl = null)  // ? Otherwise this shit falls over
        {
            //Debugger.Break();

            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl = returnUrl ?? Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            string referer = string.Empty;
            var abc = Request.Headers;
            foreach (var entry in abc)
            {
                if (entry.Key == SmartParametersV2016.refererKey)
                {
                    if (!string.IsNullOrEmpty(entry.Value))
                    {
                        // If the Referer is a mangled GUID then it shouldn't have any '/' characters
                        if (entry.Value.ToString().IndexOf(SmartParametersV2016.forwardslash) == -1)
                        {
                            referer = entry.Value;
                        }
                    }
                    break;
                }
            }

            if (!string.IsNullOrEmpty(referer))
            {
               // Even a none SmartCubeMobile login causes "Referer" to be set
               // But at this point, I have NO IDEA what is causing the Login process to start
               // It could be from EITHER the Web page OR a HTTPGET across the network ...

                string guid_challenge_key = SmartEncryptionV2016.MangleGuidKey(referer);                    
                string error_message = string.Empty;
                Aaargh.ChallengeTime = DateTime.UtcNow.ToString(SmartParametersV2016.militaryFormat, SmartParametersV2016.defaultCulture);
                // Now encrypt it with the GUID sent in the Challenge component
                string result = SmartEncryptionV2016.DoTheBiz(Aaargh.ChallengeTime, guid_challenge_key, string.Empty, true);
                // Which will be picked up on the far side (I hope)  Encode it so it gets passed intact
                if (result.Contains(SmartParametersV2016.errorPrefix))
                {
                    RaysText = string.Empty;
                }
                else
                {
                    //RaysText = System.Web.HttpUtility.UrlEncode(result);
                    RaysText = System.Net.WebUtility.UrlEncode(result);
                }
            }

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null) // To make this excrement stop stinking
        {
            //Debugger.Break();

            returnUrl = returnUrl ?? Url.Content("~/");
            if (ModelState.IsValid)
            {
                string error_message = string.Empty;
                bool found_json1 = false;
                try
                {
                    // Now check to see if a JSON string was sent in the Request Header.
                    // If it WASN'T then this request to Login is from the Website www.smartcubemobile.com or www.smartswitch.co.uk
                    //   and we don't need to send a challenge, because we send back nothing sensitive to the Website (e.g. passwords, codes, etc.)
                    // If there WAS, then this is a Login request from the GUI and we need to send a challenge because we are going to send
                    //   back (potentially) some information which might be used against us.  The challenge will identify whether we
                    //   have been connected to by a GUI which is 'ours' or by some other spoofing ginger out to fool us ...

                    var acceptHeader = Request.Headers
                                        .FirstOrDefault(h => h.Key.Equals("Accept", StringComparison.OrdinalIgnoreCase));

                    if (acceptHeader.Key != null)
                    {
                        string accept_type = acceptHeader.Value.ToString();
                        if (!string.IsNullOrEmpty(accept_type))
                        {
                            string[] accept_types = accept_type.Split(",");
                            foreach (string what in accept_types)
                            {
                                if (what.Trim() == SmartParametersV2016.applicationJson)
                                {
                                    // Yes
                                    found_json1 = true;
                                    break;
                                }
                            }
                        }
                    }
                    // Anything to do?
                    if (found_json1)
                    {
                        IHeaderDictionary abc = Request.Headers;

                        foreach (var entry in abc)
                        {
                            if (entry.Key == SmartParametersV2016.refererKey)
                            {
                                string referer = entry.Value;

                                // This GUID should change on each login attempt
                                if (!string.IsNullOrEmpty(referer))
                                {
                                    referer = referer.Replace("http://", string.Empty).TrimEnd('/');
                                    // At the moment we do THIS fudging of the GUID (but we could do anything)
                                    string guid_key = SmartEncryptionV2016.MangleGuidKey(referer);
                                    
                                    // Username is encrypted with 'GUID'
                                    string encrypted_username = Input.UserName; // ? For the reasons above
                                    Input.UserName = SmartEncryptionV2016.DoTheBiz(string.Empty, guid_key, encrypted_username, false);
                                    // Password is also encrypted with 'GUID'
                                    string encrypted_password = Input.Password;   // ? For the reasons above
                                    Input.Password = SmartEncryptionV2016.DoTheBiz(string.Empty, guid_key, encrypted_password, false);

                                    // Now ... $64,000 question did whatever is communicating with us send the correct response??
                                    string encrypted_response = Input.Raymondo;     // ? For the reasons above
                                    string decrypted_response = SmartEncryptionV2016.DoTheBiz(string.Empty, guid_key, encrypted_response, false);
                                    if (decrypted_response != Aaargh.ChallengeTime)
                                    {
                                        // Looks like whatever sent a Username/Password combo has got the challenge response wrong (or not even sent one)
                                        // So its probably a Missing or a Ruskie ginger
                                        Aaargh.ChallengeTime = string.Empty;    // Tidy up
                                        Input.UserName = Input.Password = string.Empty; // Causes the SignIn to fail
                                    }
                                    Aaargh.ChallengeTime = string.Empty;
                                    // Carry on ....
                                }
                                break;
                            }
                        }
                    }
                }
                catch
                { }

                // This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                var result = await _signInManager.PasswordSignInAsync(Input.UserName, Input.Password, Input.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");

                    if (found_json1)
                    {
                        ApplicationUser user = await _signInManager.UserManager.FindByNameAsync(Input.UserName);           // <= Put in by RAY
                    
                        Response.Clear();
                        Response.ContentType = SmartParametersV2016.applicationJson;
                        string jsonString = string.Empty;
                        // We need this to see if this is the FIRST
                        // login, so we can make a Consumer record
                        DateTime previous = user.LastLogOnTime;
                        user.LastLogOnTime = DateTime.UtcNow;                      // <= Put in by RAY
                        IdentityResult identity_result = await _signInManager.UserManager.UpdateAsync(user);  // <= Put in by RAY
                        if (identity_result.Succeeded)                          // <= Put in by RAY   
                        {
                            string time_now = user.LastLogOnTime.ToString(SmartParametersV2016.sqliteFormat); // (no need to test this)
                            int random_key = Get_Next_Random(new Random());

                            // ClearPassword is no longer stored; the legacy challenge slice is therefore empty
                            short substring = (short)(user.ClearPassword?.Length ?? 0);
                            string PassWordHash = user.PasswordHash;
                            if (PassWordHash.Length > substring)
                            {
                                PassWordHash = PassWordHash.Substring(PassWordHash.Length - substring);
                            }
                            Potential logged_in_user = new Potential()
                            {
                                Random_Key = random_key,
                                November = SmartEncryptionV2016.DoTheBiz(time_now, random_key.ToString(), string.Empty, true), // Time Now
                                // Username and all the other fields are encrypted with 'Time Now'
                                Uniform = SmartEncryptionV2016.DoTheBiz(user.UserName, time_now, string.Empty, true),                 // Username
                                Lima = SmartEncryptionV2016.DoTheBiz(Convert.ToString(user.LastLogOnTime.ToString(SmartParametersV2016.sqliteFormat)), time_now, string.Empty, true),    // Last Login Time
                                Tango = SmartEncryptionV2016.DoTheBiz(user.Trace.ToString(), time_now, string.Empty, true),           // Trace
                                Sierra = SmartEncryptionV2016.DoTheBiz(user.Subscriber.ToString(), time_now, string.Empty, true),     // Subscriber
                                Mike = SmartEncryptionV2016.DoTheBiz(user.MultipleMeter.ToString(), time_now, string.Empty, true),    // Multi-Meter
                                Alpha = SmartEncryptionV2016.DoTheBiz(user.Administrator.ToString(), time_now, string.Empty, true),   // Administrator
                                Echo = SmartEncryptionV2016.DoTheBiz(Convert.ToString(user.Expiration1.ToString(SmartParametersV2016.sqliteFormat)), time_now, string.Empty, true),     // Electricity Expiration
                                Golf = SmartEncryptionV2016.DoTheBiz(Convert.ToString(user.Expiration2.ToString(SmartParametersV2016.sqliteFormat)), time_now, string.Empty, true),     // Gas Expiration
                                Whisky = SmartEncryptionV2016.DoTheBiz(Convert.ToString(user.Expiration3.ToString(SmartParametersV2016.sqliteFormat)), time_now, string.Empty, true),   // Water Expiration
                                Indigo = SmartEncryptionV2016.DoTheBiz(user.Id, time_now, string.Empty, true),   // Special unique User identifying param
                                Papa = SmartEncryptionV2016.DoTheBiz(Convert.ToString(previous.ToString(SmartParametersV2016.sqliteFormat)), time_now, string.Empty, true),             // Previous logon
                                Charlie = PassWordHash
                            };
                            jsonString = SmartJsonV2017.JsonSerializer<Potential>(logged_in_user);
                        }
                        Response.ContentLength = jsonString.Length;
                        await Response.WriteAsync(jsonString);
                        await Response.Body.FlushAsync();                  // Flush the data to browser               
                    }
                    return LocalRedirect(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        internal static int Get_Next_Random(Random random_r)
        {
            // Gets ints in the range -2,147,483,648 to 2,147,483,647
            return random_r.Next(SmartParametersV2016.randomLowerLimit, SmartParametersV2016.randomUpperLimit);
        }
    }
}