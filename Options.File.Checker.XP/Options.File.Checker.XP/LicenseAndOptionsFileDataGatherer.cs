﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Options.File.Checker
{
    public partial class LicenseAndOptionsFileDataGatherer
    {
        // Do Regex stuff now to be efficient and stuff blah blah blah.
        private static Regex countPortEqualsRegex = new Regex("port=", RegexOptions.IgnoreCase);
        private static Regex countOptionsEquals = new Regex("options=", RegexOptions.IgnoreCase);
        private static Regex countCommentedBeginLines = new Regex("# BEGIN--------------", RegexOptions.IgnoreCase);
        private static Regex keyEquals = new Regex("key=", RegexOptions.IgnoreCase);
        private static Regex assetInfo = new Regex("asset_info=", RegexOptions.IgnoreCase);

        private static bool serverLineHasPort = true;
        private static bool daemonLineHasPort = false;
        private static bool daemonPortIsCNUFriendly = false;
        private static bool caseSensitivity = true;
        private static readonly Dictionary<int, Tuple<string, int, string, string, string>> licenseFileDictionary = new Dictionary<int, Tuple<string, int, string, string, string>>();
        private static readonly Dictionary<int, Tuple<string, string, string, string, string>> includeDictionary = new Dictionary<int, Tuple<string, string, string, string, string>>();
        private static readonly Dictionary<int, Tuple<string, string, string, string, string>> includeBorrowDictionary = new Dictionary<int, Tuple<string, string, string, string, string>>();
        private static readonly Dictionary<int, Tuple<string, string>> includeAllDictionary = new Dictionary<int, Tuple<string, string>>();
        private static readonly Dictionary<int, Tuple<string, string, string, string, string>> excludeDictionary = new Dictionary<int, Tuple<string, string, string, string, string>>();
        private static readonly Dictionary<int, Tuple<string, string, string, string, string>> excludeBorrowDictionary = new Dictionary<int, Tuple<string, string, string, string, string>>();
        private static readonly Dictionary<int, Tuple<string, string>> excludeAllDictionary = new Dictionary<int, Tuple<string, string>>();
        private static readonly Dictionary<int, Tuple<int, string, string, string, string, string>> reserveDictionary = new Dictionary<int, Tuple<int, string, string, string, string, string>>();
        private static readonly Dictionary<string, Tuple<int, string, string>> maxDictionary = new Dictionary<string, Tuple<int, string, string>>();
        private static readonly Dictionary<int, Tuple<string, string, int>> groupDictionary = new Dictionary<int, Tuple<string, string, int>>();
        private static readonly Dictionary<int, Tuple<string, string>> hostGroupDictionary = new Dictionary<int, Tuple<string, string>>();
        private static string? ErrorMessage = string.Empty;

        public static bool serverLineHasPort { get; set; }
        public static bool daemonLineHasPort { get; set; }
        public static bool daemonPortIsCNUFriendly { get; set; }
        public static bool caseSensitivity { get; set; }

        public static GatherDataResult GatherData(string licenseFilePath, string optionsFilePath)
        {
           GatherDataResult result = new GatherDataResult();
           result.Success = true;

            // I'm putting this here so that we can print its contents if we hit a generic error message.
            string line = string.Empty;

            try
            {
                // Load the file's contents.
                string[] licenseFileContentsLines = System.IO.File.ReadAllLines(licenseFilePath);
                string[] optionsFileContentsLines = System.IO.File.ReadAllLines(optionsFilePath);

                string optionsFileContents = string.Join(Environment.NewLine, optionsFileContentsLines);
                string licenseFileContents = string.Join(Environment.NewLine, licenseFileContentsLines);

                // Remove Windows line breaks.
                string lineBreaksToRemove = "\\\r\n\t";
                licenseFileContents = licenseFileContents.Replace(lineBreaksToRemove, string.Empty);

                // Remove Unix line breaks.
                lineBreaksToRemove = "\\\r\n";
                licenseFileContents = licenseFileContents.Replace(lineBreaksToRemove, string.Empty);

                // Remove more Unix line breaks...
                lineBreaksToRemove = "\\\n\t";
                licenseFileContents = licenseFileContents.Replace(lineBreaksToRemove, string.Empty);

                // Remove empty space that will likely appear on Unix systems.
                string emptySpaceToRemove = "        ";
                licenseFileContents = licenseFileContents.Replace(emptySpaceToRemove, string.Empty);

                // Put it back together!
                licenseFileContentsLines = licenseFileContents.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                // Next, let's check for some obvious errors.
                // Make sure, you know, the files exist.
                if (!System.IO.File.Exists(licenseFilePath) || !System.IO.File.Exists(optionsFilePath))
                {
                    result.ErrorMessage = "The license and/or the options file you selected either no longer exists or you don't have permissions to read one of them.";
                    result.Success = false;
                    return result;
                }

                // Error time again, in case you decided to be sneaky and close the program or manually enter the filepath.
                if (string.IsNullOrWhiteSpace(optionsFileContents))
                {
                    result.ErrorMessage = "There is an issue with the license file: it is either empty or only contains white space.";
                    result.Success = false;
                    return result;
                }

                // Make sure you actually picked an options file.
                if (!optionsFileContents.Contains("INCLUDE") && !optionsFileContents.Contains("EXCLUDE") && !optionsFileContents.Contains("RESERVE")
                    && !optionsFileContents.Contains("MAX") && !optionsFileContents.Contains("LINGER") && !optionsFileContents.Contains("LOG") &&
                    !optionsFileContents.Contains("TIMEOUT"))
                {
                    result.ErrorMessage = "There is an issue with the options file: it is likely not an options file or contains no usable content.";                    
                    result.Success = false;
                    return result;
                }

                if (!System.IO.File.ReadAllText(licenseFilePath).Contains("INCREMENT"))
                {
                    result.ErrorMessage = "There is an issue with the license file: it is either not a license file or is corrupted.";
                    result.Success = false;
                    return result;
                }

                if (licenseFileContents.Contains("lo=IN") || licenseFileContents.Contains("lo=DC") || licenseFileContents.Contains("lo=CIN"))
                {
                    result.ErrorMessage = "There is an issue with the license file: it contains an Individual or Designated Computer license, " +
                        "which cannot use an options file.";
                    result.Success = false;
                    return result;
                }

                if (System.IO.File.ReadAllText(licenseFilePath).Contains("CONTRACT_ID="))
                {
                    result.ErrorMessage = "There is an issue with the license file: it contains at least 1 non-MathWorks product.";
                    result.Success = false;
                    return result;
                }

                // Reset everything!
                serverLineHasPort = true;
                daemonLineHasPort = false;
                licenseFileDictionary.Clear();
                includeDictionary.Clear();
                includeBorrowDictionary.Clear();
                includeAllDictionary.Clear();
                excludeDictionary.Clear();
                excludeBorrowDictionary.Clear();
                excludeAllDictionary.Clear();
                reserveDictionary.Clear();
                maxDictionary.Clear();
                groupDictionary.Clear();
                hostGroupDictionary.Clear();
                result.ErrorMessage = string.Empty;

                // Stuff that the class won't output.
                bool containsPLP = false;
                int serverLineCount = 0;
                int daemonLineCount = 0;
                bool productLinesHaveBeenReached = false;

                // License file information gathering.
                for (int licenseLineIndex = 0; licenseLineIndex < licenseFileContentsLines.Length; licenseLineIndex++)
                {
                    line = licenseFileContentsLines[licenseLineIndex];
                    string productName = string.Empty;
                    int seatCount = 0;
                    string plpLicenseNumber = string.Empty;

                    if (line.TrimStart().StartsWith("SERVER"))
                    {
                        // SERVER lines should come before the product(s).
                        if (productLinesHaveBeenReached)
                        {
                            result.ErrorMessage = "There is an issue with the license file: your SERVER line(s) are listed after a product.";
                            result.Success = false;
                        }
                        serverLineCount++;

                        string[] lineParts = line.Split(' ');
                        string serverWord = lineParts[0];
                        string serverHostID = lineParts[2];

                        if (serverHostID == "27000" || serverHostID == "27001" || serverHostID == "27002" || serverHostID == "27003" || serverHostID == "27004" || serverHostID == "27005" )
                        {
                            result.ErrorMessage = "There is an issue with the license file: you have likely omitted your Host ID and attempted to specify a SERVER port number. " +
                                "Because you have omitted the Host ID, the port you've attempted to specify will not be used.";
                            result.Success = false;
                            return result;
                        }

                        if (lineParts.Length < 3)
                        {
                            result.ErrorMessage = "There is an issue with the license file: you are missing information from your SERVER line.";
                            result.Success = false;
                            return result;
                        }
                        else if (lineParts.Length == 3)
                        {
                            serverLineHasPort = false;
                        }
                        else if (lineParts.Length == 4)
                        {
                            int serverPort;

                            // Check to make sure you're using a port number.
                            if (!int.TryParse(lineParts[3], out serverPort))
                            {
                                result.ErrorMessage = "There is an issue with the license file: you have stray information on your SERVER line.";
                                result.Success = false;
                                return result;
                            }

                            if (serverWord != "SERVER")
                            {
                                result.ErrorMessage = "There is an issue with the license file: it does not start with the word SERVER.";
                                result.Success = false;
                                return result;
                            }

                            if (!serverHostID.Contains("INTERNET=") && serverHostID.Length != 12)
                            {
                                result.ErrorMessage = "There is an issue with the license file: you have not specified your Host ID correctly.";
                                result.Success = false;
                                return result;
                            }

                            // Congrats, you /may/ have not made any mistakes on your SERVER line.
                        }
                        else if (lineParts.Length == 5)
                        {
                            if (lineParts[4] == "")
                            {
                                continue; // Your stray space shall be ignored... this time.
                            }
                            else
                            {
                                result.ErrorMessage = "There is an issue with the license file: you have stray information on your SERVER line.";
                                result.Success = false;
                                return result;
                            }
                        }
                        else
                        {
                            result.ErrorMessage = "There is an issue with the license file: you have stray information on your SERVER line.";
                            result.Success = false;
                            return result;
                        }

                        // There is no situation where you should have more than 3 SERVER lines.
                        if (serverLineCount > 3 || serverLineCount == 2)
                        {
                            result.ErrorMessage = "There is an issue with the license file: it has too many SERVER lines. Only 1 or 3 are accepted.";
                            result.Success = false;
                            return result;
                        }

                    }
                    else if (line.TrimStart().StartsWith("DAEMON"))
                    {
                        // DAEMON line should come before the product(s).
                        if (productLinesHaveBeenReached)
                        {
                            result.ErrorMessage = "There is an issue with the license file: your DAEMON line is listed after a product.";
                            result.Success = false;
                            return result;
                        }

                        // There should only be one DAEMON line.
                        daemonLineCount++;
                        if (daemonLineCount > 1)
                        {
                            result.ErrorMessage = "There is an issue with the license file: you have more than 1 DAEMON line.";
                            result.Success = false;
                            return result;
                        }

                        // port= and options= should only appear once.
                        int countPortEquals = countPortEqualsRegex().Matches(line).Count;
                        int countOptionsEquals = LicenseAndOptionsFileDataGatherer.countOptionsEquals().Matches(line).Count;
                        int countCommentedBeginLines = LicenseAndOptionsFileDataGatherer.countCommentedBeginLines().Matches(line).Count;

                        // For the CNU kids.
                        if (line.Contains("PORT="))
                        {
                            daemonPortIsCNUFriendly = true;
                        }

                        if (countCommentedBeginLines > 0)
                        {
                            result.ErrorMessage = "There is an issue with the license file: it has content that is intended to be commented out in your DAEMON line.";
                            result.Success = false;
                            return result;
                        }

                        if (countPortEquals > 1)
                        {
                            result.ErrorMessage = "There is an issue with the license file: you have specified more than 1 port number for MLM.";
                            result.Success = false;
                            return result;
                        }

                        if (countOptionsEquals > 1)
                        {
                            result.ErrorMessage = "There is an issue with the license file: you have specified the path to more than 1 options file.";
                            result.Success = false;
                            return result;
                        }

                        if (countOptionsEquals == 0)
                        {
                            result.ErrorMessage = "There is an issue with the license file: you did not specify the path to the options file. " +
                                "If you included the path, but did not use options= to specify it, MathWorks licenses ask that you do so, even if they technically work without options=.";
                            result.Success = false;
                            return result;
                        }

                        // daemonProperty1 and 2 could either be a port number or path to an options file.
                        string[] lineParts = line.Split(' ');

                        // Just having the word "DAEMON" isn't enough.
                        if (lineParts.Length == 1)
                        {
                            result.ErrorMessage = "There is an issue with the license file: you have a DAEMON line, but did not specify the daemon to be used (MLM) nor the path to it.";
                            result.Success = false;
                            return result;
                        }

                        // Checking for the vendor daemon MLM.
                        string daemonVendor = lineParts[1];

                        if (string.IsNullOrWhiteSpace(daemonVendor))
                        {
                            result.ErrorMessage = "There is an issue with the license file: there are too many spaces between \"DAEMON\" and \"MLM\".";
                            result.Success = false;
                            return result;
                        }

                        // The vendor daemon needs to MLM. Not mlm or anything else.
                        if (daemonVendor != "MLM")
                        {
                            result.ErrorMessage = "There is an issue with the license file: you have incorrectly specified the vendor daemon MLM.";
                            result.Success = false;
                            return result;
                        }

                        // Just specifying "DAEMON MLM" isn't enough.
                        if (lineParts.Length == 2)
                        {
                            result.ErrorMessage = "There is an issue with the license file: you did not specify the path to the vendor daemon MLM.";
                            result.Success = false;
                            return result;
                        }

                        // You're missing your options file path.
                        if (lineParts.Length == 3)
                        {
                            result.ErrorMessage = "There is an issue with the license file: you did not specify the path to the options file.";
                            result.Success = false;
                            return result;
                        }

                        if (countPortEquals == 1)
                        {
                            daemonLineHasPort = true;
                        }
                    }
                    // Where the product information is found.
                    else if (line.TrimStart().StartsWith("INCREMENT"))
                    {
                        productLinesHaveBeenReached = true;
                        string[] lineParts = line.Split(' ');
                        productName = lineParts[1];
                        int productVersion = int.Parse(lineParts[3]);
                        string productExpirationDate = lineParts[4];
                        string productKey = lineParts[6];
                        string licenseOffering = string.Empty;
                        string licenseNumber = string.Empty;
                        _ = int.TryParse(lineParts[5], out seatCount);
                        string rawSeatCount = lineParts[5];

                        // License number.
                        string pattern = @"asset_info=([^\s]+)";

                        if (line.Contains("asset_info="))
                        {
                            Regex regex = new Regex(pattern);
                            Match match = regex.Match(line);

                            if (match.Success)
                            {
                                licenseNumber = match.Groups[1].Value;
                            }
                        }
                        else if (line.Contains("SN="))
                        {
                            pattern = @"SN=([^\s]+)";
                            Regex regex = new Regex(pattern);
                            Match match = regex.Match(line);

                            if (match.Success)
                            {
                                licenseNumber = match.Groups[1].Value;
                            }
                            if (productName == "TMW_Archive") // Welcome to the land of PLPs!
                            {
                                containsPLP = true;
                                plpLicenseNumber = licenseNumber;
                                continue;
                            }
                        }
                        else if (containsPLP && productName.Contains("PolySpace")) // This is the best guess we can make if you're using a PLP-era product.
                        {
                            licenseNumber = plpLicenseNumber;
                        }
                        else
                        {
                            result.ErrorMessage = "There is an issue with the license file: the license number " + licenseNumber + " was not found for the product " + productName + ".";
                            result.Success = false;
                            return result;
                        }

                        // License offering.
                        if (line.Contains("lo="))
                        {
                            if (line.Contains("lo=CN:"))
                            {
                                licenseOffering = "lo=CN";
                            }
                            else if (line.Contains("lo=CNU"))
                            {
                                licenseOffering = "CNU";
                            }
                            else if (line.Contains("lo=NNU"))
                            {
                                licenseOffering = "NNU";
                            }
                            else if (line.Contains("lo=TH"))
                            {
                                if (!line.Contains("USER_BASED="))
                                {
                                    licenseOffering = "lo=CN";
                                }
                                else
                                {
                                    result.ErrorMessage = "There is an issue with the license file: it is formatted incorrectly. " +
                                        productName + "'s license offering is being read as Total Headcount, but also Network Named User, which doesn't exist.";
                                    result.Success = false;
                                    return result;
                                }
                            }
                            else
                            {
                                result.ErrorMessage = "There is an issue with the license file: the product " + productName + " has an invalid license offering.";
                                result.Success = false;
                                return result;
                            }
                        }
                        else if (line.Contains("lr=") || containsPLP && !line.Contains("asset_info=")) // Figure out your trial or PLP's license offering.
                        {
                            if (seatCount > 0)
                            {
                                if (line.Contains("USER_BASED"))
                                {
                                    licenseOffering = "NNU";
                                }
                                else
                                {
                                    if (containsPLP && !line.Contains("asset_info="))
                                    {
                                        licenseOffering = "lo=DC"; // See PLP-era explaination below.
                                    }
                                    else
                                    {
                                        licenseOffering = "lo=CN";
                                    }
                                }
                            }
                            // This means you're likely using a macOS or Linux PLP-era license, which CAN use an options file... I think it has to.
                            else if (containsPLP && !line.Contains("asset_info="))
                            {
                                licenseOffering = "lo=IN";
                                seatCount = 1;
                            }
                            else
                            {
                                result.ErrorMessage = "There is an issue with the license file: the product " + productName + " comes from an Individual " +
                                    "or Designated Computer license, which cannot use an options file.";
                                result.Success = false;
                                return result;
                            }
                        }
                        else
                        {
                            if (line.Contains("PLATFORMS=x"))
                            {
                                result.ErrorMessage = "There is an issue with the license file: the product" + productName + " comes from an Individual " +
                                     "or Designated Computer license generated from a PLP on Windows, which cannot use an options file.";
                            }
                            else
                            {
                                result.ErrorMessage = "There is an issue with the license file: the product" + productName + " has an valid license offering.";
                            }
                            result.Success = false;
                            return result;
                        }

                        // Check the product's expiration date. Year 0000 means perpetual.
                        if (productExpirationDate == "01-jan-0000")
                        {
                            productExpirationDate = "01-jan-2999";
                        }

                        // Convert/parse the productExpirationDate string to a DateTime object.
                        DateTime expirationDate = DateTime.ParseExact(productExpirationDate, "dd-MMM-yyyy", CultureInfo.InvariantCulture);

                        // Get the current system date.
                        DateTime currentDate = DateTime.Now.Date;

                        if (expirationDate < currentDate)
                        {
                            result.ErrorMessage = "There is an issue with the license file: The product" + productName + " on license number " +
                                + licenseNumber + " expired on " + productExpirationDate + ". Please update your license file appropriately before proceeding.";
                            result.Success = false;
                            return result;
                        }

                        if (licenseOffering.Contains("NNU"))
                        {
                            if (seatCount != 1 && !containsPLP)
                            {
                                seatCount /= 2;
                            }
                        }

                        // Technically infinite. This should avoid at least 1 unnecessary error report.
                        if (licenseOffering.Contains("CNU") && (seatCount == 0))
                        {
                            seatCount = 9999999;
                        }

                        if (licenseOffering == "lo=CN" && (seatCount == 0) && licenseNumber == "220668")
                        {
                            if ((productVersion <= 18) || (productName.Contains("Polyspace") && productVersion <= 22))
                            {
                                result.ErrorMessage = "There is an issue with the license file: it contains a Designated Computer or Individual license" + licenseNumber + ".";
                            }
                            else
                            {
                                result.ErrorMessage = "There is an issue with the license file: it contains a Designated Computer license" + licenseNumber + " , " +
                                    "that is incorrectly labeled as a Concurrent license.";
                            }
                            result.Success = false;
                            return result;
                        }

                        if (!licenseOffering.Contains("CNU") && rawSeatCount == "uncounted")
                        {
                            result.ErrorMessage = "There is an issue with the license file: it contains an Individual or Designated Computer license, " +
                                "which cannot use an options file. The license number is question is " + licenseNumber + ".";
                            result.Success = false;
                            return result;
                        }

                        if (seatCount < 1 && line.Contains("asset_info="))
                        {
                            result.ErrorMessage = "There is an issue with the license file: " + productName + " on license " + licenseNumber + " is reading with a seat count of zero or less.";
                            result.Success = false;
                            return result;
                        }

                        // Before proceeding, make sure the values we've collected are valid.
                        if (string.IsNullOrWhiteSpace(productName))
                        {
                            result.ErrorMessage = "There is an issue with the license file: a product name is being detected as blank on " + licenseNumber + ".";
                            result.Success = false;
                            return result;
                        }

                        if (licenseNumber.Contains("broken") || string.IsNullOrWhiteSpace(licenseNumber) || Regex.IsMatch(licenseNumber, @"^[^Rab_\d]+$"))
                        {
                            result.ErrorMessage = "There is an issue with the license file: an invalid license number" + licenseNumber + " , is detected for " + productName + ".";
                            result.Success = false;
                            return result;
                        }

                        if (licenseOffering.Contains("broken") || string.IsNullOrWhiteSpace(licenseOffering))
                        {
                            result.ErrorMessage = "There is an issue with the license file: a license offering could not be detected for " + productName + " " +
                                "on license number " + licenseNumber + ".";
                            result.Success = false;
                            return result;
                        }

                        if (string.IsNullOrWhiteSpace(productKey))
                        {
                            result.ErrorMessage = "There is an issue with the license file: a product key could not be detected for " + productName + " on license number " + licenseNumber + ".";
                            result.Success = false;
                            return result;
                        }

                        licenseFileDictionary[licenseLineIndex] = Tuple.Create(productName, seatCount, productKey, licenseOffering, licenseNumber);
                    }
                }

                // Options file information gathering.
                for (int optionsLineIndex = 0; optionsLineIndex < optionsFileContentsLines.Length; optionsLineIndex++)
                {
                    line = optionsFileContentsLines[optionsLineIndex];

                    if (line.TrimStart().StartsWith("INCLUDE ") || line.TrimStart().StartsWith("INCLUDE_BORROW ") || line.TrimStart().StartsWith("EXCLUDE ") || line.TrimStart().StartsWith("EXCLUDE_BORROW "))
                    {
                        string optionType = string.Empty;

                        if (line.TrimStart().StartsWith("INCLUDE ")) { optionType = "INCLUDE"; }
                        else if (line.TrimStart().StartsWith("INCLUDE_BORROW ")) { optionType = "INCLUDE_BORROW"; }
                        else if (line.TrimStart().StartsWith("EXCLUDE ")) { optionType = "EXCLUDE"; }
                        else if (line.TrimStart().StartsWith("EXCLUDE_BORROW ")) { optionType = "EXCLUDE_BORROW"; }

                        string[] lineParts = line.Split(' ');

                        // Stop putting in random spaces.
                        while (string.IsNullOrWhiteSpace(lineParts[0]) && lineParts.Length > 1)
                        {
                            lineParts = lineParts.Skip(1).ToArray();
                        }

                        if (lineParts.Length < 4)
                        {
                            result.ErrorMessage = "There is an issue with the options file: you have an incorrectly formatted " + optionType + " line. It is missing necessary information. " +
                                "The line in question is \"" + line + "\".";
                            result.Success = false;
                            return result;
                        }

                        string productName = lineParts[1];
                        string licenseNumber;
                        string productKey;
                        string clientType;
                        string clientSpecified;

                        if (productName.Contains('"'))
                        {
                            // Check for stray quotation marks.
                            int quoteCount = line.Count(c => c == '"');
                            if (quoteCount % 2 != 0)
                            {
                                result.ErrorMessage = "There is an issue with the options file: one of your " + optionType + " lines has a stray quotation mark. " +
                                    "The line in question reads as this: " + line;
                                result.Success = false;
                                return result;
                            }

                            productName = productName.Replace("\"", "");
                            licenseNumber = lineParts[2];
                            if (!productName.Contains(':'))
                            {
                                if (licenseNumber.Contains("key=", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    productKey = lineParts[2];
                                    string unfixedProductKey = productKey;
                                    string quotedProductKey = KeyEquals().Replace(unfixedProductKey, "");
                                    productKey = quotedProductKey.Replace("\"", "");
                                    licenseNumber = string.Empty;
                                }
                                else // asset_info=
                                {
                                    string unfixedLicenseNumber = licenseNumber;
                                    string quoteLicenseNumber = AssetInfo().Replace(unfixedLicenseNumber, "");
                                    licenseNumber = quoteLicenseNumber.Replace("\"", "");
                                    productKey = string.Empty;
                                }

                                clientType = lineParts[3];

                                if (clientType != "USER" && clientType != "GROUP" && clientType != "HOST" && clientType != "HOST_GROUP" && clientType != "DISPLAY" &&
                                clientType != "PROJECT" && clientType != "INTERNET")
                                {
                                    result.ErrorMessage = "There is an issue with the options file: you have incorrectly specified the client type on a line using " + optionType + "." +
                                        "You attempted to use " + clientType + ". Please reformat this " + optionType + " line.";
                                    result.Success = false;
                                    return result;
                                }

                                clientSpecified = string.Join(" ", lineParts.Skip(4)).TrimEnd();
                            }
                            else // If you have " and :
                            {
                                string[] colonParts = productName.Split(":");
                                if (colonParts.Length != 2)
                                {
                                    result.ErrorMessage = "There is an issue with the options file: one of your " + optionType + " lines has a stray colon for " + productName + ".";
                                    result.Success = false;
                                    return result;
                                }
                                productName = colonParts[0];
                                if (colonParts[1].Contains("key="))
                                {
                                    string unfixedProductKey = colonParts[1];
                                    productKey = unfixedProductKey.Replace("key=", "");
                                    licenseNumber = string.Empty;
                                }
                                else
                                {
                                    string unfixedLicenseNumber = colonParts[1];
                                    licenseNumber = Regex.Replace(unfixedLicenseNumber, "asset_info=", "", RegexOptions.IgnoreCase);
                                    productKey = string.Empty;
                                }
                                clientType = lineParts[2];
                                clientSpecified = string.Join(" ", lineParts.Skip(3)).TrimEnd();
                            }
                        }                        
                        else if (productName.Contains(':')) // In case you decided to use a : instead of ""...
                        {
                            string[] colonParts = productName.Split(":");
                            if (colonParts.Length != 2)
                            {
                                result.ErrorMessage = "There is an issue with the options file: one of your " + optionType + " lines has a stray colon for " + productName + ".";
                                result.Success = false;
                                return result;
                            }
                            productName = colonParts[0];
                            if (colonParts[1].Contains("key="))
                            {
                                string unfixedProductKey = colonParts[1];
                                productKey = unfixedProductKey.Replace("key=", "");
                                licenseNumber = string.Empty;
                            }
                            else
                            {
                                string unfixedLicenseNumber = colonParts[1];
                                licenseNumber = Regex.Replace(unfixedLicenseNumber, "asset_info=", "", RegexOptions.IgnoreCase);
                                productKey = string.Empty;
                            }
                            clientType = lineParts[2];
                            clientSpecified = string.Join(" ", lineParts.Skip(3)).TrimEnd();
                        }
                        else
                        {
                            clientType = lineParts[2];
                            clientSpecified = string.Join(" ", lineParts.Skip(3)).TrimEnd();
                            licenseNumber = string.Empty;
                            productKey = string.Empty;
                        }

                        if (line.TrimStart().StartsWith("INCLUDE ")) { includeDictionary[optionsLineIndex] = Tuple.Create(productName, licenseNumber, productKey, clientType, clientSpecified); }
                        else if (line.TrimStart().StartsWith("INCLUDE_BORROW ")) { includeBorrowDictionary[optionsLineIndex] = Tuple.Create(productName, licenseNumber, productKey, clientType, clientSpecified); }
                        else if (line.TrimStart().StartsWith("EXCLUDE ")) { excludeDictionary[optionsLineIndex] = Tuple.Create(productName, licenseNumber, productKey, clientType, clientSpecified); }
                        else if (line.TrimStart().StartsWith("EXCLUDE_BORROW ")) { excludeBorrowDictionary[optionsLineIndex] = Tuple.Create(productName, licenseNumber, productKey, clientType, clientSpecified); }
                    }
                    else if (line.TrimStart().StartsWith("INCLUDEALL ") || line.TrimStart().StartsWith("EXCLUDEALL "))
                    {
                        string optionSpecified = string.Empty; // Could either be INCLUDEALL or EXCLUDEALL.
                        string clientType; // Examples include GROUP or USER.                    
                        string clientSpecified = string.Empty; // Examples include "matlab_group" or "root".
                        string[] lineParts = line.Split(' ');

                        // Stop putting in random spaces.
                        while (string.IsNullOrWhiteSpace(lineParts[0]) && lineParts.Length > 1)
                        {
                            lineParts = lineParts.Skip(1).ToArray();
                        }

                        if (line.TrimStart().StartsWith("INCLUDEALL ")) { optionSpecified = "INCLUDEALL"; }
                        else if (line.TrimStart().StartsWith("EXCLUDEALL ")) { optionSpecified = "EXCLUDEALL"; }

                        if (lineParts.Length < 3)
                        {
                            result.ErrorMessage = "There is an issue with the options file: you have an incorrectly formatted " + optionSpecified + "line. It is missing necessary information. " +
                                "The line in question is \"" + line + "\".";
                            result.Success = false;
                            return result;
                        }

                        clientType = lineParts[1];
                        clientSpecified = string.Join(" ", lineParts.Skip(2));

                        if (clientType != "USER" && clientType != "GROUP" && clientType != "HOST" && clientType != "HOST_GROUP" && clientType != "DISPLAY" &&
                            clientType != "PROJECT" && clientType != "INTERNET")
                        {
                            result.ErrorMessage = "There is an issue with the options file: you have incorrectly specified the client type on an " + optionSpecified +
                                " line as \"" + clientType + "\". Please reformat this " + optionSpecified + "line's client type to something such as \"USER\".";
                            result.Success = false;
                            return result;
                        }

                        if (line.TrimStart().StartsWith("INCLUDEALL ")) { includeAllDictionary[optionsLineIndex] = Tuple.Create(clientType, clientSpecified); }
                        else if (line.TrimStart().StartsWith("EXCLUDEALL ")) { excludeAllDictionary[optionsLineIndex] = Tuple.Create(clientType, clientSpecified); }
                    }
                    else if (line.TrimStart().StartsWith("MAX "))
                    {
                        string[] lineParts = line.Split(' ');

                        // Stop putting in random spaces.
                        while (string.IsNullOrWhiteSpace(lineParts[0]) && lineParts.Length > 1)
                        {
                            lineParts = lineParts.Skip(1).ToArray();
                        }

                        if (lineParts.Length < 5)
                        {
                            result.ErrorMessage = "There is an issue with the options file: you have an incorrectly formatted MAX line. It is missing necessary information. " +
                                "The line in question is \"" + line + "\".";
                            result.Success = false;
                            return result;
                        }

                        int maxSeats = int.Parse(lineParts[1]);
                        string maxProductName = lineParts[2];
                        string maxClientType = lineParts[3];
                        string maxClientSpecified = string.Join(" ", lineParts.Skip(4));

                        maxDictionary[maxProductName] = Tuple.Create(maxSeats, maxClientType, maxClientSpecified);
                    }
                    else if (line.TrimStart().StartsWith("RESERVE "))
                    {
                        string[] lineParts = line.Split(' ');

                        // Stop putting in random spaces.
                        while (string.IsNullOrWhiteSpace(lineParts[0]) && lineParts.Length > 1)
                        {
                            lineParts = lineParts.Skip(1).ToArray();
                        }

                        if (lineParts.Length < 5)
                        {
                            result.ErrorMessage = "There is an issue with the options file: you have an incorrectly formatted RESERVE line. It is missing necessary information. " +
                                "The line in question is \"" + line + "\".";
                            result.Success = false;
                            return result;
                        }

                        // Check for stray quotation marks.
                        int quoteCount = line.Count(c => c == '"');
                        if (quoteCount % 2 != 0)
                        {
                            result.ErrorMessage = "There is an issue with the options file: one of your RESERVE lines has a stray quotation mark. " +
                                "The line in question reads as this: " + line + "";
                            result.Success = false;
                            return result;
                        }

                        string reserveSeatsString = lineParts[1];
                        string reserveProductName = lineParts[2];
                        string reserveLicenseNumber;
                        string reserveProductKey;
                        string reserveClientType;
                        string reserveClientSpecified;

                        // Convert the seat count from a string to a integer.
                        int reserveSeatCount = 0;

                        if (int.TryParse(reserveSeatsString, out reserveSeatCount))
                        {
                            // Parsing was successful.
                        }
                        else
                        {
                            result.ErrorMessage = "There is an issue with the options file: you have incorrectly specified the seat count for one of your RESERVE lines. " +
                                "You either chose an invalid number or specified something other than a number.";
                            result.Success = false;
                            return result;
                        }

                        if (reserveSeatCount <= 0)
                        {
                            result.ErrorMessage = "There is an issue with the options file: you specified a RESERVE line with a seat count of 0 or less... why?";
                            result.Success = false;
                            return result;
                        }

                        if (reserveProductName.Contains('"'))
                        {
                            reserveProductName = reserveProductName.Replace("\"", "");
                            reserveLicenseNumber = lineParts[3];
                            if (!reserveProductName.Contains(':'))
                            {
                                if (reserveLicenseNumber.Contains("key="))
                                {
                                    reserveProductKey = lineParts[3];
                                    string unfixedReserveProductKey = reserveProductKey;
                                    string quotedReserveProductKey = unfixedReserveProductKey.Replace("key=", "");
                                    reserveProductKey = quotedReserveProductKey.Replace("\"", "");
                                    reserveLicenseNumber = string.Empty;
                                }
                                // asset_info=
                                else
                                {
                                    string unfixedReserveLicenseNumber = reserveLicenseNumber;
                                    string quoteReserveLicenseNumber = Regex.Replace(unfixedReserveLicenseNumber, "asset_info=", "", RegexOptions.IgnoreCase);
                                    reserveLicenseNumber = quoteReserveLicenseNumber.Replace("\"", "");
                                    reserveProductKey = string.Empty;
                                }

                                reserveClientType = lineParts[4];
                                reserveClientSpecified = string.Join(" ", lineParts.Skip(5)).TrimEnd();
                            }
                            // If you have " and :
                            else
                            {
                                string[] colonParts = reserveProductName.Split(":");
                                if (colonParts.Length != 2)
                                {
                                    result.ErrorMessage = "There is an issue with the options file: one of your RESERVE lines has a stray colon for " + reserveProductName + ".";
                                    result.Success = false;
                                    return result;
                                }
                                reserveProductName = colonParts[0];
                                if (colonParts[1].Contains("key="))
                                {
                                    string unfixedReserveProductKey = colonParts[1];
                                    reserveProductKey = unfixedReserveProductKey.Replace("key=", "");
                                    reserveLicenseNumber = string.Empty;
                                }
                                else
                                {
                                    string unfixedReserveLicenseNumber = colonParts[1];
                                    reserveLicenseNumber = Regex.Replace(unfixedReserveLicenseNumber, "asset_info=", "", RegexOptions.IgnoreCase); reserveProductKey = string.Empty;
                                }
                                reserveClientType = lineParts[3];
                                reserveClientSpecified = string.Join(" ", lineParts.Skip(4)).TrimEnd();
                            }
                        }
                        // In case you decided to use a : instead of ""...
                        else if (reserveProductName.Contains(':'))
                        {
                            string[] colonParts = reserveProductName.Split(":");
                            if (colonParts.Length != 2)
                            {
                                result.ErrorMessage = "There is an issue with the options file: one of your RESERVE lines has a stray colon for " + reserveProductName + ".";
                                result.Success = false;
                                return result;
                            }
                            reserveProductName = colonParts[0];
                            if (colonParts[1].Contains("key="))
                            {
                                string unfixedReserveProductKey = colonParts[1];
                                reserveProductKey = unfixedReserveProductKey.Replace("key=", "");
                                reserveLicenseNumber = string.Empty;
                            }
                            else
                            {
                                string unfixedReserveLicenseNumber = colonParts[1];
                                reserveLicenseNumber = Regex.Replace(unfixedReserveLicenseNumber, "asset_info=", "", RegexOptions.IgnoreCase); reserveProductKey = string.Empty;
                            }
                            reserveClientType = lineParts[3];
                            reserveClientSpecified = string.Join(" ", lineParts.Skip(4)).TrimEnd();
                        }
                        else
                        {
                            reserveClientType = lineParts[3];
                            reserveClientSpecified = string.Join(" ", lineParts.Skip(4)).TrimEnd();
                            reserveLicenseNumber = string.Empty;
                            reserveProductKey = string.Empty;
                        }
                        reserveDictionary[optionsLineIndex] = Tuple.Create(reserveSeatCount, reserveProductName, reserveLicenseNumber, reserveProductKey, reserveClientType, reserveClientSpecified);
                    }
                    else if (line.TrimStart().StartsWith("GROUP "))
                    {
                        string[] lineParts = line.Split(' ');

                        // Stop putting in random spaces.
                        while (string.IsNullOrWhiteSpace(lineParts[0]) && lineParts.Length > 1)
                        {
                            lineParts = lineParts.Skip(1).ToArray();
                        }

                        if (lineParts.Length < 3)
                        {
                            result.ErrorMessage = "There is an issue with the options file: you have an incorrectly formatted GROUP line. It is missing necessary information. " +
                                "The line in question is \"" + line + "\".";
                            result.Success = false;
                            return result;
                        }

                        string groupName = lineParts[1];
                        string groupUsers = string.Join(" ", lineParts.Skip(2)).TrimEnd();
                        int groupUserCount = groupUsers.Split(' ').Length;

                        groupDictionary[optionsLineIndex] = Tuple.Create(groupName, groupUsers, groupUserCount);
                    }
                    else if (line.TrimStart().StartsWith("HOST_GROUP "))
                    {
                        string[] lineParts = line.Split(' ');

                        // Stop putting in random spaces.
                        while (string.IsNullOrWhiteSpace(lineParts[0]) && lineParts.Length > 1)
                        {
                            lineParts = lineParts.Skip(1).ToArray();
                        }

                        if (lineParts.Length < 3)
                        {
                            result.ErrorMessage = "There is an issue with the options file: you have an incorrectly formatted HOST_GROUP line. It is missing necessary information. " +
                                "The line in question is \"" + line + "\".";
                            result.Success = false;
                            return result;
                        }

                        string hostGroupName = lineParts[1];
                        string hostGroupClientSpecified = string.Join(" ", lineParts.Skip(2));

                        hostGroupDictionary[optionsLineIndex] = Tuple.Create(hostGroupName, hostGroupClientSpecified);
                    }
                    else if (line.TrimStart().StartsWith("GROUPCASEINSENSITIVE ON"))
                    {
                        caseSensitivity = false;
                    }                    
                    else if (line.TrimStart().StartsWith("TIMEOUTALL ") || line.TrimStart().StartsWith("DEBUGLOG ") || line.TrimStart().StartsWith("LINGER ") || line.TrimStart().StartsWith("MAX_OVERDRAFT ") 
                        || line.TrimStart().StartsWith("REPORTLOG ") || line.TrimStart().StartsWith("TIMEOUT ") || line.TrimStart().StartsWith("BORROW ") || line.TrimStart().StartsWith("NOLOG ") 
                        || line.TrimStart().StartsWith("DEFAULT ") || line.TrimStart().StartsWith("HIDDEN ") || line.TrimStart().StartsWith("#") || line == "")
                    {
                        // Other valid line beginnings that I currently do nothing with.
                    }
                    else // This should help spot my stupid typos.
                    {
                        result.ErrorMessage = "There is an issue with the options file: you have started a line with an unrecognized option. Please make sure you didn't make any typos. " +
                            "The line in question's contents are: \"" + line + "\".";
                        result.Success = false;
                        return result;
                    }
                }

                return result; // Success!
            }
            catch (Exception ex)
            {
                if (ex.Message == "The value cannot be an empty string. (Parameter 'path')")
                {
                    result.ErrorMessage = "You left the license or options file text field blank.";
                }
                else if (ex.Message == "Index was outside the bounds of the array.")
                {
                    result.ErrorMessage = "There is a formatting issue in your license/options file. This is the line in question's contents: \"" + line + "\"";
                }
                else
                {
                    result.ErrorMessage = "You managed to break something. How? Here's the automatic message: " + ex.Message;
                }

                result.Success = false;
                return result;
            }
        }
    }
}
