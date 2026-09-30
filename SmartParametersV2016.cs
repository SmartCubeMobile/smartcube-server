// Copyright © 2010-2027 Principal Research Corporation Ltd. All rights reserved.
// No part of this software may be reproduced, stored in a retrieval system, or transmitted
// in any form or by any means, electronic, mechanical, photocopying, recording, or otherwise,
// without the prior written permission of Principal Research Corporation Ltd.

using System;
using System.Globalization;
using System.Reflection;

#if SMARTMAUI
using Microsoft.Maui.Controls.PlatformConfiguration;

#endif

namespace SmartCubeMobile
{
    //public enum UtilityResource : int
    //{
    //    Electricity = 'E',            // From Hezbollah.resources
    //    Gas = 'G'                     // Ditto   
    //}
    public class SmartParametersV2016
    {
        internal static int toastDuration = 3;  // Seconds?

        // Make this EMPTY!! for PRODUCTION!!!
        internal const string myUsername = "";
        internal const string myPassword = "";

        // This is in English - I know ...
        internal const string catastrophe = "Dictionary load failure";

        internal const string href = "href";

        // Of course... it HAD to be a float ...!
        internal const float NationwideBrowserTimeout = 30000 * 2;
        internal const string fourdecplaces = "{0:0.0000}";

        internal const string MainDatabase = "SmartSwitch";
        internal const int SmartSwitch = 4;
        internal const int SmartUsers = 1;

        internal const int ECBCheckTimer = 600000;   // 10 minutes

        public const string SourcePath = @"C:\Users\Ray\Documents\Visual Studio 2022\Source Code\";
        public const string ECBWebAddress = "http://www.ecb.europa.eu/stats/eurofxref/eurofxref-hist-90d.xml";
        public const string WINFORMS = "WINFORMS";
        public const string WPF = "WPF";
        // From Maui DevicePlatform
        public const string Android = "Android";
        public const string iOS = "iOS";
        public const string MacCatalyst = "MacCatalyst";
        public const string WinUI = "WinUI";
        public const string Tizen = "Tizen";

        internal const string XRPNETWORK = "ripple";
        internal const string ETHNETWORK = "Etherium";

        internal const string BanksProvidersName = "Providers";
        internal const string SavingsProvidersName = "Providers";
        internal const string InvestmentsProvidersName = "Providers";
        internal const string LoansProvidersName = "Providers";
        internal const string CreditCardsProvidersName = "Providers";
        internal const string CryptosProvidersName = "Wallets";

        internal const string institutionprompt = "Select Institution";
        internal const string providersprompt = "Select Providers";
        internal const string accountsprompt = "Select Accounts";
        internal const string transactionsprompt = "Select Transaction Types";
        internal const string financeInstitutionsMessage = "FinanceInstitutions";
        internal const string financeProvidersMessage = "FinanceProviders";
        internal const string financeAccountsMessage = "FinanceAccounts";

        internal const string supplierprompt = "Select Supplier";
        internal const string tariffprompt = "Select Tariff";
        internal const string paymentplanprompt = "Select PaymentPlan";
        internal const string utilitySuppliersMessage = "UtilitySuppliers";
        internal const string utilityTariffsMessage = "UtilityTariffs";
        internal const string utilityPaymentPlansMessage = "UtilityPaymentPlans";

        public const int randomLowerLimit = -2147483648; // This includes -2,147,483,648
        public const int randomUpperLimit = 2147483647;  // This only includes 2147483646 ??
        internal static readonly CultureInfo defaultCulture = new CultureInfo("en-GB");
        internal static readonly CultureInfo cultureEUR = new CultureInfo("fr-FR");
        internal static readonly CultureInfo cultureUSD = new CultureInfo("en-US");
        internal static readonly string defaultISOConvertToSymbol = "GBP";
        internal const bool totalBalance = true;
        internal const bool availableBalance = false;

        internal const string none = "          <none>           ";
        internal const char Electricity = 'E';
        internal const char Gas = 'G';
        internal const char DualFuel = 'D';
        internal const char Banks = 'B';
        internal const char Investments = 'I';
        internal const char Savings = 'S';
        internal const char Cryptos = 'K';

        internal const char Finance = 'F';
        internal const char Insurance = 'I';
        internal const char Profiles = 'P';
        internal const char Utility = 'U';

        internal const int keepaliveLimit = 60;    // 60 seconds
        internal const int doorCount = 25;  // 100 is fine WITHOUT Background  Make sure this is LESS than the Meter Top height!!
        internal const int doorTicks = 10;  // 10 msecs is fine WITHOUT a background image,
                                            // but 5msecs is necessary WITH a background image
                                            // in order to get some speed up!!

        internal const int keysExcludingUsername = 8; // 1 to 8 is eight fields
        internal const int keysIncludingUsername = 9; // 0 to 8 is nine fields

        internal const string defaultUoM = "kWh";
        internal const string guidFormat = "D";
        internal const string connectSymbol = "G";
        internal const string disconnectSymbol = "g";

        internal const short maximumBankAccountName = 18;
        internal const short maximumBankSortCode = 6;
        internal const short maximumBankAccountNumber = 8;

        internal const string errorPrefix = "Error: ";
        internal const int minimumMPANLength = 19;      // Max 21 ??
        internal const int minimumMPRNLength = 6;       // Max 10
        internal const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        internal const string websiteLogin = "/Identity/Account/Login";
        internal const string adminEmailAddress = "admin@keasdon.co.uk";

        internal const string unknownMPAN = "MPAN Unknown";
        internal const string unknownMPRN = "MPRN Unknown";

        internal const short currentVatCode = 2;            // Points to 5.00% as we speak
        internal const int energylinxCommentsColour = 47;   // Purple

        internal const char DEFAULTECONOMY7 = 'N';
        internal static readonly string[] months = {"January", "February", "March",
                                                        "April", "May", "June", "July",
                                                        "August", "September", "October",
                                                        "November", "December"};
        internal static readonly string[] dates = {" Jan ", " Feb ", " Mar ",
                                                        " Apr ", " May ", " Jun ",
                                                        " Jul ", " Aug ", " Sep ",
                                                        " Oct ", " Nov ", " Dec "};

        internal const string defaultConvertToSymbol = "£";

        internal const char defaultDenominationSymbol = 'p';
        internal const char defaultCurrencySeparator = '.';
        internal const char defaultThousandsSeparator = ',';
        internal const int SUPPLIERNAMELENGTH = 40;
        internal const int TARIFFNAMELENGTH = 100;
        internal const int SELECTIONNAMELENGTH = 70;
        internal const int GROUPNAMELENGTH = 80;
        internal const int PAYMENTTYPESLENGTH = 30;
        internal const int TYPESLENGTH = 50;
        internal const int NAMESLENGTH = 50;
        internal const int METERSERIALLENGTH = 14;

        internal const string VOLUMECORRECTION = "1.02264";  // Default value - should make no difference
        internal const string KWHCONVERSION = "3.6";         // ditto above

        internal const string https = "https://";
        internal const string KEASDONENERGYLTD = "©Keasdon Energy Ltd";
#if WINFORMS
        internal const string uswitchWebsite = "https://www.uswitch.com/";
#endif
        internal const int HeaderFontSize = 10;
        internal const int BodyFontSize = 8;

        //#if Z!PRODUCTION
#if ANDROIDX || ANDROID
#if EMULATOR
        internal const string localWebsite = "http://10.0.2.2";
#endif
        // The tablet can resolve this by blasting out to it
        // via the server, which the PC picks up via IIS on its port
        internal const string localWebsite = "http://192.168.0.137";   // 'Settings' CHAPMANS IPVFour Address (not IPV$ DNS address)  

        // The Android tablet has no means of resolving this via DNS or anything
        // so it gives up. Windows, however can resolve it via DNS  
        //internal const string localWebsite = "http://CHAPMANS";
#endif
#if !(ANDROIDX || ANDROID)
        internal const string localWebsite = "http://localhost";
#endif
        internal const string remoteWebsite = "https://www.keasdon.co.uk";
        //#endif

        //#if PRODUCTION
        //        internal const string website = "https://www.keasdon.co.uk";
        //        internal int serverTimeoutSecsx = 60;        // DON'T FORGET TO TAKE THE ZERO BACK
        //#Xelse
        internal static int serverTimeoutSecs = 120;        // This is checked in SmartNibby.CheckTimeout!!
        internal static int serverTimeoutMsecs = serverTimeoutSecs * 1000;
        //#endif
        internal static double TimeoutSecs = Convert.ToDouble(serverTimeoutSecs);
        internal static TimeSpan timespanTimeout = TimeSpan.FromSeconds(TimeoutSecs);
        internal static int otpTimeout = 2;     // Minutes
        internal static int commandTimeout = 60;

        internal static int cryptoRatesLimit = 1200;  // 1200secs = 20minutes      // Exchange Rates refresh
                                                      //#if PRODUCTION
                                                      //        internal const string companyDomain = "www.keasdon.co.uk";
                                                      //        internal const string productionDomain = "www.smartswitch.co.uk";
                                                      //#endif
                                                      //#if Z!PRODUCTION
                                                      //        internal const string companyDomain = @".\";
                                                      //        internal const string productionDomain = @".\";
                                                      //#endif

        internal const string prodCompanyDomain = "www.keasdon.co.uk";
        internal const string prodProductDomain = "www.smartswitch.co.uk";
        internal const string devCompanyDomain = @".\";
        internal const string devProductDomain = @".\";

        internal const string localsystem = "http://CHAPMANS:80/";

        internal const string keystone = "Smart%";        // For Databases AND Schemas!
        internal const string pipename = "DBServer";
        internal const bool clone = true;

        internal const string TotalTables = "T";
        internal const string SingleProcedure = "Z";

        internal const string wildcard = "*";
        internal const string requestParameter = "Request";
        internal const string responseParameter = "Response";
        internal const string zeroParameter = "0";
        internal const string firstParameter = "1";
        internal const string secondParameter = "2";
        internal const string thirdParameter = "3";
        internal const string fourthParameter = "4";
        internal const string fifthParameter = "5";
        internal const string sixthParameter = "6";
        internal const string seventhParameter = "7";
        internal const string usernameParameter = "Username";
        internal const string databaseParameter = "Database";
        internal const string tableParameter = "Table";
        internal const string sqlParameter = "SQL";
        internal const string p1Parameter = "P1";
        internal const string operationParameter = "Operation";
        internal const string procedureParameter = "Procedure";
        internal const string guidParameter = "Guid";
        internal const string timeNowParameter = "Time_Now";
        internal const string methodParameter = "Method";
        internal const string mimeParameter = "MIME";
        internal const string targetParameter = "Target";
        internal const string userAgentParameter = "User_Agent";
        internal const string dnoParameter = "DNO";

        internal static string serverName = ".";      // Used in the .ashx website files
        internal const char operationFailure = 'X';
        internal const char operationUnsure = '?';
        internal const char operationSuccess = '*';  // Should be a tick, but for
                                                     // the time being we will make do
                                                     // with a green star!  Otherwise
                                                     // we have to change the console's
                                                     // front and that's way too much pain
        //internal const char operationIUDSuccess = '#';  // I'm running out of chars!!!!
                                                        // Its so I can distinguish a IUD
                                                        // operation which is followed by
                                                        // a Consumer record

        internal const string contentType = "text/plain";
        internal const string contentImage = "image/png";
        internal const string requestVerificationToken = "__RequestVerificationToken";

        // Replace Database= with databaseName
        //#if PRODUCTION
        //        internal static string connectionString = "Server=.\\;" + //SQLEXPRESS;" +       // This is the instance - its STILL SQL Server 2014!
        //                                        "Trusted_Connection=yes;" +
        //                                        "Database=;";
        //#Xelse
        internal static string connectionString = "Server=.\\;" + // This is MSSQLSERVER ? The default instance
                                    "Trusted_Connection=yes;" +
                                    "Database=;Encrypt=False";
        //#endif

        internal const char defaultAutoswitch = 'O';        // In case we get 2 bills with the same date for different resources
        internal const char defaultBillprfix = 'Y';        // In case we get 2 bills with the same date for different resources
        internal const char defaultResourceCode = '\0';
        //internal const char defaultFinanceAccountTypeCode = Banks;
        internal const char defaultUtilityResourceCode = 'E';
        internal const char crossResourceCode = 'X';
        internal const string defaultUtilityResourceType = "SR";
        internal const string standardFormat = "dd/MM/yyyy";
        internal const string militaryFormat = "dd/MM/yyyy HH:mm:ss";
        internal const string sqliteFormat = "yyyy-MM-dd HH:mm:ss";
        internal const string ddmmmyyyyFormat = "dd-MMM-yyyy";
        internal const string sqldateFormat = "yyyy-MM-dd HH:mm:ss.fff";
        internal const string dashesFormat = "yyyy-MMM-dd";
        internal const string shortFormat = "yyyy-MM-dd";
        internal const string yymmddFormatX = "yyyy/MM/dd HH:mm:ss";        // Should make it 24-hour format?
        internal const string defaultDates = "01/01/1900 00:00:00";
        internal const string sqldefaultdates = "1900-01-01 00:00:00";
        internal static DateTime defaultDate = SmartTimeV2016.ConvertDateTime(defaultDates);
        internal static DateTime defaultMaxdate = SmartTimeV2016.ConvertDateTime(maximumDate);
        internal static string basicDates = "01/01/0001 00:00:00";
        internal static DateTime basicDate = SmartTimeV2016.ConvertDateTime(basicDates);
        internal const string yymmddShortFormat = "yyyy/MM/dd";
        internal static TimeSpan utcdefaultOffset = new TimeSpan(0, 0, 0, 0, 0);
        internal static TimeSpan oneDay = new TimeSpan(1, 0, 0, 0, 0);  // 24 hours
        internal static TimeSpan oneSec = new TimeSpan(0, 0, 0, 1, 0);
        internal static int clockInt = 1000;
#if ANDROIDX
        internal static double clockInterval = 1000;        // 1 sec
        internal static long doorDuration = 5000;           // Door duration                          // 
#endif
#if WINFORMS || WPF  || WINUI || MAUI
        internal static double clockInterval = 1000;       // 1000 millisecs = 1 second
#endif
        internal const string format0Places = "{0:0}",
                                format2Places = "{0:0.00}",
                                format3Places = "{0:0.000}",
                                currencyFormat = "{0:C}";

        internal const string serverTraceFilename = "SmartDBServer.txt";

        // These separators are taken from http://web.itu.edu.tr/~sgunduz/courses/mikroisl/ascii.html
        // 28 1C FILE SEPARATOR(FS) RIGHT ARROW 
        // 29 1D GROUP SEPARATOR(GS) LEFT ARROW 
        // 30 1E RECORD SEPARATOR(RS) UP ARROW 
        // 31 1F UNIT SEPARATOR(US) DOWN ARROW

        internal const char fieldSeparator = '\x11';     // DC1 XON
        internal const char dc2 = '\x12';                // DC2
        internal const char dc3 = '\x13';                // DC3 (unused)
        internal const char dc4 = '\x14';                // DC4 (unused)

        internal const char encryptSeparator = '\x1A';   // SUB (for time_now + encrypted_data fields i.e combined fields)
        internal const char ourSeparator = '\x1B';    // Escape
        internal const char fileSeparator = '\x1C';    // RIGHT ARROW
        internal const char groupSeparator = '\x1D';    // LEFT ARROW
        internal const char recordSeparator = '\x1E';    // UP ARROW
        internal const char unitSeparator = '\x1F';    // DOWN ARROW
        
        internal const string transactionOn = "+";       // plus
        internal const string transactionOff = "-";      // minus

        internal const string ampersand = "&";
        internal const string underscore = "_";
        internal const string dash = "-";
        internal const string minus = "-";
        internal const string plus = "+";
        internal const string comma = ",";
        internal const string forwardslash = @"/";
        internal const string decimalPoint = ".";
        internal const string leftParenthesis = "(";
        internal const string equivalent = "=";
        internal const char commachar = ',';
        internal const char colon = ':';
        internal const char semiColon = ';';
        internal const string space = " ";
        internal const char spacechar = ' ';
        internal const string period = ".";
        internal const string percent = "%";
        internal const string billDateDelimiter = "/";
        internal const string billDateSeparator = "-";
        internal const char sortcode = '+';
        internal const char bar = '|';
        internal const char equalsSplit = '=';


        internal const char spaceSplit = ' ';
        internal const char dashSplit = '-';
        internal const char newline = '\n';
        internal const char carriageReturn = '\r';
        internal const char tab = '\t';

        internal const char daytimeUnit = 'D';
        internal const char nighttimeUnit = 'N';

        internal const string maximumDate = "31/12/9998 00:00:00";
        internal const string sensibleStartingDate = "01/01/2000";
        internal const short zeroRateVatCode = 1;      // From VatRates list

        internal static bool displayLowercaseTables = false;
        internal static bool displayServerMessage = false;

        internal const string accessInfo = "Allowing requests from senders: ";
        internal const string pipeIdentities = "SYSTEM, IIS APPPOOL\\DefaultAppPool"; //"SYSTEM";
                                                                                      //#if PRODUCTION
                                                                                      //        internal static string[] permittedSenders = new string[2] {"SYSTEM", "Keasdon_Production"};
                                                                                      //#Xelse
        internal static string[] permittedSenders = { "SYSTEM", "DefaultAppPool" };
        //#endif
        internal const string displayInfo = "Displaying requests from senders: ";

        //#if PRODUCTION
        //        internal const string permittedDomain = prodProductDomain; // <= And of course you can always encrypt and decrypt this ..
        //#Xelse
        internal const string permittedDomain = devProductDomain; // <= And of course you can always encrypt and decrypt this ..

        //#endif
        internal const string permittedReferer = "SmartDashboard"; // <= And of course you can always encrypt and decrypt this ..
        internal const string pdfDownloadPath = @"C:\Temp";

        internal const string refererKey = "Referer";
        internal const int redirect302s = 10;

        //internal const char defaultLastDisplay = 'X';
        internal const char defaultAutoSwitch = 'N';
        internal const string defaultUsername = "BOB";

        internal const string initialNextRoutine = "Contact";
        internal const string yesFlag = "Y";
        internal const string noFlag = "N";
#if WINFORMS
        internal const string schemaprfix = "SmartCubeMobile";    // <= SHOULD ALWAYS MATCH THE NAMESPACE
        internal static string activeStatus = "";
        internal const string deletedStatus = "D";
        internal const string lockedStatus = "L";
        internal const char placeholderDesignation = '^';
        //internal const char deletedDesignation = '~';
        internal const char defaultgreen = '¬';
        internal const char defaultred = '~';

#endif
        internal const string tcrDefault = "n/a";

        internal const char propogateTariffs = 'Y';
        internal const char propogateTariffsDefault = 'N';
        internal const char brandMatrixValid = 'Y';
        internal const char brandMatrixInvalid = 'N';
        internal const char tariffMatrixValid = 'Y';
        internal const char tariffMatrixInvalid = 'N';
        internal const bool active = true;  // <= No bolleans in SQLite!!
        internal const bool inactive = false;
        internal const char activeFlag = 'Y';               // For Brands we CAN scrape
        internal const char activeDefault = 'N';       // For BRands we CAN'T scrape

        internal const char accountActiveFlag = 'Y';
        internal const string lastChecked = "X";
        internal const string unChecked = "";

        internal const short defaultVersionCode = 1;

        internal const string dataDelimiter = "'";
        internal const string delimiterSubstitute = @""""; // Should put double quote in

        internal const string update = "U";

        internal const string defaultExcelSpreadsheet = "SMARTUTILITY";
        internal const string defaultSchemaKeyword = "Smart%";
        //internal const string smartswitchExcelSpreadsheet = MainProcess.textBoxSmartSwitchFilename.Text;

        internal const string sectionsExactMatch = "X";
        internal const string sectionsCanOverride = "Y";

        internal const char defaultChar = '\0';
        // These are all in [master] stored procedures
        internal const string uspFindAllDatabases = "usp_FIND_ALL_DATABASES";
        internal const string findAllProcedures = "FIND_ALL_PROCEDURES";
        internal const string findAllParameters = "FIND_ALL_PARAMETERS";
        internal const string findAllSchemas = "FIND_ALL_SCHEMAS";
        internal const string findAllTables = "FIND_ALL_TABLES";
        internal const string findAllPrimaryKeys = "FIND_ALL_PRIMARY_KEYS";
        internal const string findAllColumns = "FIND_ALL_COLUMNS";
        internal const string findAllProperties = "FIND_ALL_PROPERTIES";

        internal const string applicationJson = "application/json";
        internal const string serverFailedToRespond = "Server failed to respond - please contact Support";
        internal const string challengeFailed = "Challenge failed - please contact Support";
        internal const string websiteFailedToRespond = "Website failed to respond - please contact Support";
        internal const string loginDatabaseFailed = "Login database connection failed - please contact Support";
        internal const string unableToAuthenticate = "Unable to authenticate - you are not logged-in";

        internal const short defaultPaymentDay = 1;

        public const string SmartSwitchFilename = "SMARTSWITCH.db3";
        public const string DatabaseFilename = "SMARTCUBEDATA.db3";
        internal const string targetDirectory = "SmartCube";

        internal const char singleaccounttype = 'S';
        internal const char multipleaccounttype = 'M';
        internal const char accountopen = 'Y';

        internal const string SmartUsersSchema = "SmartUsers";
        internal const string SmartProfileSchema = "SmartProfile";
        internal const string SmartFinanceSchema = "SmartFinance";
        internal const string SmartUtilitySchema = "SmartUtility";
        internal static bool CrashExit = false;

        internal const char AccountStatus = 'O';    // Open
    }
}