//EPX Visa payment script
//Author Tom Strohecker
/*Script Description
 * Script that reads in the EPX visa files and creates an fdr.tap file to be send over to PSCU
 * Since this script will read in two files at a time an Archive directory was created to store each file after processing
 * 
 * 1. Reads in all files and moves them to a processing directory
 * 2. Within the processing directoy Read in file(s)
 * 3. For each file
 *  a. Read in line
 *  b. Split lines by TAB "\t"
 *  c. Checks to see if line is a visa payment column number 29= V or v
 *  d. Subtracts the fee amount from the payment amount
 *  e. updates count and total amount variables
 *  f. formats the payment amount to fdr standards
 *  g. writes line to file
 *  h. Script will be ran via a selfservice button and will run twice daily. 5:00pm and 8:30/9:00am
 *  
 */

/*Updates
 * Author              Date                    Update
 * 
 * Will need to have script create a second file that will be able to be used as a letter file for the episys job 
 * to pick up and perform the transactions on.
 * 
 * Will need to be able to read the data from each file and potentially create two seperate files for episys,
 * one for each file ACH and CC
 * 
 * FILES MAY COME IN DIFFERENT FORMATS SO THE SCRIPT WILL NEED TO VARIFIY THE FILE TYPE BEFORE PROCESSING 
 * 
 */

/*File format **MUST INCLUDE FULL VISA CARD NUMBER IN FILE FOR CREATION OF FDR.TAP FILE
 *  
 */


/*Notes: Two file column locations will change once Same day file is produced.
 * CCRECON
 *     -CHECK FIELD F COL 5 FOR A 2 ELSE IGNORE.
 * CCSETTLE
 *     -IGNORE
 * ACHSETTLE
 *     -SHOULD HAVE ALL INFORMATION    
 */

/*Update due to potential Collections issue
 * 
 * Member's payments could be late due to file being a settlement file. 
 * Thus this will need to be a sameday file that will post transactions day of payment
 * Might no longer need to create second batch posting job for ACH share transactions
 * 
 * Currently reading in two files of different format this may change as well.
 * 
 * Two additional reports which will be sent into synergy will be created. One for visa and one 
 * for SL transactions.
 * 
 */

/*Update 10/24/2018
 * Script will still read in two seperate files containing small differences in the 
 * columns. Both files will also contain all the required information
 * 
 * Update 03/26/2019 - Tom Strohecker
 *  Added logic to account for smaller data files for the CCRECON
 * 
 * Update 08/01/2019 - Tom Strohecker
 *  Added logic to account for Visa to mastercard converstion types
 *  
 *Update 10/23/2019 -Tom Strohecker  
 *  Added Logic to split the totals for Mastercard and visa card on the output report
 *  
 *Update 12/02/2019- Tom Strohecker 
 *  Added logic to account for issue when validating if credit card payment for CC file
 *
 *Update 04/4/2023 - Tom Strohecker 
 * Refactored code, updateded logic to account for file layout changes  
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using EPX_File_Script;



namespace EPX_File_Script
{
    class Program
    {
        static String BannerPageCreation(string category, string title)
        {
            //Creation of Local Variables
            /*
             * The reports title is passed in the function call.
             * Stores the systems Current Date and Time in the variables SysDate
             * Stores the current month in the variable SysMon
             * Stores the current day in the variable SysDay
             * Creates the formatted string of the date by referencing the variables
             * Creates the formatted banner page string and passes it back to main where it is stored in BannerText
             */
            
            String BannerText;
            String TempText;
            DateTime SysDate = DateTime.Now;
            int SysMon = SysDate.Month;
            int SysDay = SysDate.Day;
            string Dte = SysMon.ToString("D2") + "/" + SysDay.ToString("D2") + "/" + SysDate.Year.ToString("D4");

            BannerText = "**BP**" + category;
            TempText = AddSpacing(BannerText, "C");
            BannerText = TempText + title;
            TempText = AddSpacing(BannerText, "T");
            BannerText = TempText + Dte;
            return BannerText;
        }
        static String AddSpacing(String BP, String Cat)
        {
            int NumbSp = BP.Length;
            String NewStr = BP;
            
            if (Cat == "C")
            {
                for (int i = NumbSp; i < 42; i++)
                {
                    NewStr += " ";
                }
            }
            else if (Cat == "T")
            {
                for (int i = NumbSp; i < 83; i++)
                {
                    NewStr += " ";
                }
            }
            return NewStr;
        }

        static void Main()
        {

            //Directory Paths
//            string SourceDirectory = @"C:\FileTransfers\Incoming_Files\EPX\";//Start folder of file
//            string ProcessingDirectory = @"C:\FileTransfers\Incoming_Files\EPX\Processing\";//Processing Directory
//            string ArchiveDirectory = @"C:\FileTransfers\Incoming_Files\EPX\Archive\";//Archive Directory after processing finishes

            //Directory Paths
            string SourceDirectory = @"Z:\Projects\In Progress\2023\EPX\Files\";//Start folder of file
            string ProcessingDirectory = @"Z:\Projects\In Progress\2023\EPX\Files\Process\";//Processing Directory
            string ArchiveDirectory = @"Z:\Projects\In Progress\2023\EPX\Files\Archive\";//Archive Directory after processing finishes

            string Line;   
            string ParsedLine;
            string basefee = "10"; //Base fee value
            string feeamt = "10.00"; //Base fee monetary value
            string[] poschar = { "{", "A", "B", "C", "D", "E", "F", "G", "H", "I" };
            bool Heading = false;
            string Filename;//file name variable used for creating and changing file
            string Destination;//sets the path of file
            DateTime today = DateTime.Today;

            //Header and Trailer variables
            string Line1 = "159961000" + today.ToString("MMddyy") + "Y1   0001   Y" + "                                                   ";
            string Line2 = "2403946100055557    1                                                          ";
            string Line8 = "8403946100055557 ";
            string Line9 = "959961000";
            string NewLine1 = "181503000" + today.ToString("MMddyy") + "Y1   0001   Y" + "                                                   ";
            string NewLine2 = "2514059300055555    1                                                          ";
            string NewLine8 = "8514059300055555 ";
            string NewLine9 = "981503000";

            //Synergy Files
            string datestamp = DateTime.Now.ToString("MMddyyyy hhmm");
            string OpticalFile = SourceDirectory + "EPX Credit Card Payments" + datestamp + ".txt";
            string OpticalPayment = SourceDirectory + "EPX SHLN Payments" + datestamp + ".txt";
            string OpticalHeader = "AccountNumber          Payment Amount           Fee Amount        Date";
            double visatot = 0.00;
            double CCtotal = 0.00;
            double CCFee = 0.00;
            double visafee = 0.00;
            double SLTot = 0.00;
            double SLFee = 0.00;
            int accountLoc = -1;
            int dateLoc = -1;
            int paymentAmountLoc = -1;
            int feeAmountLoc = -1;
            int statusLoc = -1;
            int paymentTypeLoc = -1;
            bool fieldsMapped;
            //OLD AND NEW VISA FILE VARIABLES
            int VisaCount;
            double VisaAmount;
            int OldVisaCount;
            double OldVisaAmount;
            string OldFileName = SourceDirectory + "oldfile.txt";
            string VisaFilename = SourceDirectory + "Visafdr.tap" + ".txt";
            string Fname = SourceDirectory + "fdr.tap.txt";

            try
            {
                string[] Sfiles = Directory.GetFiles(SourceDirectory);
                string testfile = SourceDirectory + "EPX.TRAN.txt";
                string ExceptionFile = SourceDirectory + "Exception File_" + today + ".txt";
                
                foreach (string f in Sfiles)
                {
                    Filename = System.IO.Path.GetFileName(f);
                    Destination = System.IO.Path.Combine(ProcessingDirectory, Filename);
                    System.IO.File.Move(f, Destination);
                    Console.WriteLine("___ {0} was moved to {1}", System.IO.Path.GetFileNameWithoutExtension(f), ProcessingDirectory);
                }

                string[] Pfiles = Directory.GetFiles(ProcessingDirectory);//holds all files moved to processing directory
                VisaCount = 0;
                VisaAmount = 0;
                OldVisaCount = 0;
                OldVisaAmount = 0;

                foreach (string f in Pfiles)//get the file from new folder
                {
                    if (f.IndexOf("_S") <= 0 && f.IndexOf("RECON") > 0)
                    {
                        Filename = SourceDirectory + "fdr.tap" + ".txt";

                        if (f.IndexOf("ACHRECON") > 0)//achrecon
                        {
                            fieldsMapped = false;

                            foreach (var i in File.ReadLines(f))
                            {
                                Transaction transaction = new Transaction();
                                Line = i;
                                Heading = WriteHeader(Heading, OldFileName, VisaFilename, Line1, Line2, NewLine1, NewLine2);
                                ParsedLine = RemoveCharacters(Line);
                                string[] columns = ParsedLine.Split('\t');

                                if (fieldsMapped == false)
                                {
                                    fieldsMapped = GetFieldLocations(columns, ref accountLoc, ref dateLoc, 
                                        ref paymentAmountLoc, ref feeAmountLoc, ref statusLoc, ref paymentTypeLoc);
                                }

                                transaction.Account = columns[accountLoc];
                                transaction.FeeAmount = columns[feeAmountLoc];
                                transaction.PaymentAmount = columns[paymentAmountLoc];
                                transaction.Status = columns[statusLoc];
                                transaction.PostDate = columns[dateLoc];
                                transaction.PaymentType = paymentTypeLoc == -1 ? "" : columns[paymentTypeLoc];
                                transaction.FileType = "A";

                                ProcessPayment(transaction, basefee, feeamt, SourceDirectory, VisaFilename, OldFileName, OpticalFile, OpticalHeader, poschar, i,
                                    ref visatot, ref CCtotal, ref visafee, ref CCFee, ref VisaAmount, ref OldVisaAmount, ref VisaCount, ref OldVisaCount, ref SLTot, ref SLFee, ref Filename);
                            }
                        }//ACHRECON
                        else if (f.IndexOf("CCRECON") > 0)
                        {
                            fieldsMapped = false;

                            foreach (var i in File.ReadLines(f))
                            {
                                Transaction transaction = new Transaction();
                                Line = i;
                                Heading = WriteHeader(Heading, OldFileName, VisaFilename, Line1, Line2, NewLine1, NewLine2);
                                ParsedLine = RemoveCharacters(Line);
                                string[] columns = ParsedLine.Split('\t');

                                if (fieldsMapped == false)
                                {
                                    fieldsMapped = GetFieldLocations(columns, ref accountLoc, ref dateLoc,
                                        ref paymentAmountLoc, ref feeAmountLoc, ref statusLoc, ref paymentTypeLoc);
                                }

                                transaction.Account = columns[accountLoc]; 
                                transaction.FeeAmount = columns[feeAmountLoc]; 
                                transaction.PaymentAmount = columns[paymentAmountLoc];
                                transaction.Status = columns[statusLoc]; 
                                transaction.PostDate = columns[dateLoc];
                                transaction.PaymentType = paymentTypeLoc == -1 ? "" : columns[paymentTypeLoc];
                                transaction.FileType = "C";

                                ProcessPayment(transaction, basefee, feeamt, SourceDirectory, VisaFilename, OldFileName, OpticalFile, OpticalHeader, poschar, i,
                                    ref visatot, ref CCtotal, ref visafee, ref CCFee, ref VisaAmount, ref OldVisaAmount, ref VisaCount, ref OldVisaCount, ref SLTot, ref SLFee, ref Filename);
                            }
                        }//CCRECON

                        //move file to archive folder after it has finished being processed
                        String MoveFile = System.IO.Path.GetFileName(f);
                        Destination = System.IO.Path.Combine(ArchiveDirectory, MoveFile);
                        Destination = Destination[0..^4] + datestamp + ".txt";
                        System.IO.File.Move(f, Destination);
                        Console.WriteLine("___ {0} was moved to {1}", System.IO.Path.GetFileNameWithoutExtension(f), ArchiveDirectory);
                    }
                }

                if (File.Exists(OpticalFile))
                {
                    using StreamWriter sw = File.AppendText(OpticalFile);
                    sw.WriteLine("\n");
                    sw.WriteLine("Total Visa Payment: " + visatot.ToString("0.00"));
                    sw.WriteLine("Total Visa Fee: " + visafee.ToString("0.00"));
                    sw.WriteLine("Total Visa Pay-Fee: " + (visatot - visafee).ToString("0.00"));
                    sw.WriteLine("\n");
                    sw.WriteLine("Total Non-Visa Payment: " + CCtotal.ToString("0.00"));
                    sw.WriteLine("Total Non-Visa Fee: " + CCFee.ToString("0.00"));
                    sw.WriteLine("Total Non-Visa Pay-Fee: " + (CCtotal - CCFee).ToString("0.00"));
                }
                if (File.Exists(OpticalPayment))
                {
                    using StreamWriter sw = File.AppendText(OpticalPayment);
                    sw.WriteLine("\n");
                    sw.WriteLine("Total Payment: " + SLTot.ToString());
                    sw.WriteLine("Total Fee: " + SLFee.ToString());
                    sw.WriteLine("Total Pay-Fee: " + (SLTot - SLFee).ToString());
                }

                WriteTap(VisaFilename, NewLine8, NewLine9, VisaAmount, VisaCount, poschar);
                WriteTap(OldFileName, Line8, Line9, OldVisaAmount, OldVisaCount, poschar);
                MergeFile(OldFileName, VisaFilename, Fname);
            }
            catch (Exception e)
            {
                Console.WriteLine("The process failed: {0}", e.ToString());
            }
        }
        private static void WriteTap(string FName, string L8, string L9, double Amt, int C, string[] poschar)
        {
            //format the count variable for the trailer lines
            string VCount = Convert.ToString(C);
            int num = Convert.ToInt16(VCount.Substring(VCount.Length - 1, 1));
            VCount = VCount[0..^1] + poschar[num];
            
            while (VCount.Length < 7)
            {
                VCount = "0" + VCount;
            }
            
            //format the amount variable for the trailer lines
            string VAmount = Convert.ToString(Amt);
            num = Convert.ToInt16(VAmount.Substring(VAmount.Length - 1, 1));
            VAmount = VAmount[0..^1] + poschar[num];
            
            while (VAmount.Length < 9)
            {
                VAmount = "0" + VAmount;
            }

            //write the trailer lines to the file
            L8 = L8 + VCount + VAmount + "                                              ";
            L9 = L9 + "00" + VCount + "00" + VAmount + "                                                  ";
            WriteToFile(FName, L8);
            WriteToFile(FName, L9);

            if (C == 0)
            {
                File.Delete(FName);
            }
            return;
        }
        private static void MergeFile(string P1, string P2, string F)
        {
            string P1Text = "";
            string P2text = "";
            
            if (File.Exists(P1))
            {
                P1Text = File.ReadAllText(P1);
                File.Delete(P1);
            }
            
            if (File.Exists(P2))
            {
                P2text = File.ReadAllText(P2);
                File.Delete(P2);
            }

            File.WriteAllText(F, P1Text + P2text);
            return;
        }

        private static void WriteToFile(string FName, string Line)
        {
            if (!File.Exists(FName))//create the new file
            {
                using StreamWriter sw = new StreamWriter(FName, true);
                sw.WriteLine(Line);
            }
            else//append file after it is created
            {
                using StreamWriter sw = File.AppendText(FName);
                sw.WriteLine(Line);
            }

            return;
        }

        public static bool WriteHeader(bool Heading, string OldFileName, string VisaFilename, string Line1, string Line2, string NewLine1, string NewLine2) 
        {
            if (Heading == false)
            {
                WriteToFile(OldFileName, Line1);
                WriteToFile(OldFileName, Line2);
                WriteToFile(VisaFilename, NewLine1);
                WriteToFile(VisaFilename, NewLine2);
                Heading = true;
            }

            return Heading;
        }

        public static string RemoveCharacters(string Line) 
        {
            if (Line.Contains("\"")) { Line = Line.Replace("\"" + "," + "\"", "\t"); }
            if (Line.Contains("\"")) { Line = Line.Replace("\"", ""); }
            if (Line.Contains("'")) { Line = Line.Replace("'", ""); }

            return Line;
        }

        public static void ProcessPayment(Transaction transaction, string basefee, string feeamt,string SourceDirectory, string VisaFilename, string OldFileName, string OpticalFile,
                                            string OpticalHeader, string[] poschar, string i, ref double visatot, ref double CCtotal, ref double visafee, ref double CCFee,
                                            ref double VisaAmount, ref double OldVisaAmount, ref int VisaCount, ref int OldVisaCount, ref double SLTot, ref double SLFee, ref string FileName) 
        {
            string datestamp = DateTime.Now.ToString("MMddyyyy hhmm");
            string OpticalPayment = SourceDirectory + "EPX SHLN Payments" + datestamp + ".txt";
            bool result = double.TryParse(transaction.Account, out _);
            string testfile = SourceDirectory + "EPX.TRAN.txt";
            transaction.VisaFlag = false;
            transaction.NewVisaFlag = false;

            if (transaction.Account != "" && result == true)
            {
                if (transaction.FeeAmount.Contains(basefee))
                {
                    transaction.FeeAmount = feeamt;
                }
                else
                {
                    transaction.FeeAmount = "0.00";
                }

                if ((transaction.Account.Substring(0, 1) == "4" || transaction.Account.Substring(0, 1) == "5") && transaction.Account.Length == 16)
                {
                    transaction.VisaFlag = true;
                    transaction = GetCreditCardType(transaction, VisaFilename, OldFileName, ref FileName);
                }

                bool postedTransaction = GetPostedStatus(transaction);

                if (transaction.VisaFlag == true && postedTransaction)
                {
                    PostCCPayment(transaction, OpticalFile, OpticalHeader, poschar,FileName, testfile, 
                        ref visatot, ref CCtotal, ref visafee, ref CCFee, ref VisaAmount, ref OldVisaAmount, ref VisaCount, ref OldVisaCount);
                }
                //File created will need to be placed into Episys so that the transactions can be posted to the member account
                else if (((transaction.Account.Substring(0, 1) == "1" || transaction.Account.Substring(0, 1) == "2") && postedTransaction && transaction.Account.Length == 13) || (transaction.VisaFlag))//create file to be sent to episys as letter file
                {
                    transaction.PaymentAmount = transaction.PaymentAmount.Replace("$", "");
                    transaction.FeeAmount = transaction.FeeAmount.Replace("$", "");//columns[13]

                    if (File.Exists(OpticalPayment))
                    {
                        using StreamWriter sw = File.AppendText(OpticalPayment);
                        SLTot += Convert.ToDouble(transaction.PaymentAmount);

                        if (transaction.FeeAmount != "")//col13
                        {
                            SLFee += Convert.ToDouble(transaction.FeeAmount); //col13
                        }

                        WritetoOpticalFile(sw, transaction);
                    }
                    else
                    {
                        using StreamWriter sw = new StreamWriter(OpticalPayment, true);
                        string ReportTitle = "EPX Payment Report";
                        string RepCat = "CREDEBCARD";
                        string BannerText = BannerPageCreation(RepCat, ReportTitle);
                        SLTot += Convert.ToDouble(transaction.PaymentAmount);

                        if (transaction.FeeAmount != "")
                        {
                            SLFee += Convert.ToDouble(transaction.FeeAmount);
                        }

                        sw.WriteLine(BannerText);
                        sw.WriteLine('\f');
                        sw.WriteLine(OpticalHeader);
                        WritetoOpticalFile(sw, transaction);
                    }

                    string Line = transaction.Account + "|" + transaction.PostDate + "|" + transaction.PaymentAmount + "|" + transaction.FeeAmount + "|" + transaction.FileType;
                    WriteToFile(testfile, Line);
                }
                else//create exception file 
                {
                    string ExceptionFile = SourceDirectory + "exception.txt";
                    WriteToFile(ExceptionFile, i);
                }
            }//check null account number
            else
            {
                string ExceptionFile = SourceDirectory + "exception.txt";
                WriteToFile(ExceptionFile, i);
            }
        }

        public static Transaction GetCreditCardType(Transaction transaction, string VisaFilename, string OldFileName, ref string FileName) 
        {
            if (transaction.Account.Substring(0, 1) == "5")
            {
                transaction.NewVisaFlag = true;
                FileName = VisaFilename;
            }
            else
            {
                transaction.NewVisaFlag = false;
                FileName = OldFileName;
            }

            return transaction;
        }

        public static void PostCCPayment(Transaction transaction, string OpticalFile, string OpticalHeader, string[] poschar, string FileName, string testfile,
                                                ref double visatot, ref double CCtotal, ref double visafee, ref  double CCFee, ref double VisaAmount, ref double OldVisaAmount,
                                                ref int VisaCount, ref int OldVisaCount) 
        {
            transaction.PaymentAmount = transaction.PaymentAmount.Replace("$", "");
            transaction.FeeAmount = transaction.FeeAmount.Replace("$", "");

            if (File.Exists(OpticalFile))
            {
                using StreamWriter sw = File.AppendText(OpticalFile);
                UpdateCreditCardTotals(transaction, ref visatot, ref CCtotal, ref visafee, ref CCFee);
                WritetoOpticalFile(sw, transaction);
            }
            else
            {
                CreateOpticalFile(transaction,OpticalFile, OpticalHeader, ref visatot, ref CCtotal, ref visafee, ref CCFee);
            }
            
            transaction.PaymentAmount = FormatMoneyString(transaction.PaymentAmount);
            transaction.FeeAmount = FormatMoneyString(transaction.FeeAmount);
            transaction.PaymentAmount = transaction.PaymentAmount.Replace("$", "");
            
            if (transaction.NewVisaFlag == true)
            {
                VisaAmount += (Convert.ToDouble(transaction.PaymentAmount) - Convert.ToDouble(transaction.FeeAmount));//total amount for trailer lines **subtract the fee amount from the payment line
            }
            else
            {
                OldVisaAmount += (Convert.ToDouble(transaction.PaymentAmount) - Convert.ToDouble(transaction.FeeAmount));//total amount for trailer lines **subtract the fee amount from the payment line
            }

            string vma = Convert.ToString(Convert.ToDouble(transaction.PaymentAmount) - Convert.ToDouble(transaction.FeeAmount));
            vma = vma[0..^1];
            int num = Convert.ToInt16(transaction.PaymentAmount.Substring(transaction.PaymentAmount.Length - 1, 1));//trail character for amount                                                                                                                    //lineamount=payment amount - fee amount                                                                                                            //lineamount = Convert.ToString((Convert.ToInt32(columns[9]) - Convert.ToInt32(columns[15]))).Substring(0, Convert.ToString((Convert.ToInt32(columns[9]) - Convert.ToInt32(columns[15]))).Length - 1) + poschar[num];
            string lineamount = vma + poschar[num];
           
            while (lineamount.Length < 7) 
            { 
                lineamount = "0" + lineamount; 
            }

            //Each data line will be formatted 5 + card number + date + amount + P
            bool isNumeric = long.TryParse(transaction.Account, out _);
            string temp = transaction.Account;

            while (isNumeric == false && temp.Length > 10)
            {
                temp = temp[1..].TrimStart().TrimEnd();
                isNumeric = long.TryParse(temp, out _);
            }
            
            string dataline = "5" + temp + transaction.PostDate.Substring(0, 2) + transaction.PostDate.Substring(3, 2) + transaction.PostDate.Substring(transaction.PostDate.Length - 2, 2) + lineamount + "P";
            string Line = dataline + "                                                ";
            
            if (transaction.NewVisaFlag == true) 
                VisaCount++; 
            else  
                OldVisaCount++; 
            
            WriteToFile(FileName, Line);
            Line = transaction.Account + "|" + transaction.PostDate + "|" + transaction.PaymentAmount + "|" + transaction.FeeAmount + "|" + transaction.FileType;
            WriteToFile(testfile, Line);
            return;
        }

        public static String PadString(string inputString, int charLength) 
        {
            while (inputString.Length < charLength)
            {
                inputString += " ";
            }

            return inputString;
        }

        public static string FormatMoneyString(string amount) 
        {
            if (amount.Contains("."))
                amount = amount.Replace(".", "");           
            else 
                amount += "00"; 

            return amount;
        }

        public static void CreateOpticalFile(Transaction transaction,string OpticalFile, string OpticalHeader, ref double visatot, ref double CCtotal, ref double visafee, ref double CCFee) 
        {
            using StreamWriter sw = new StreamWriter(OpticalFile, true);
            string ReportTitle = "EPX Credit Card Report";
            string RepCat = "CREDEBCARD";
            string BannerText = BannerPageCreation(RepCat, ReportTitle);
            UpdateCreditCardTotals(transaction, ref visatot, ref CCtotal, ref visafee, ref CCFee);
            sw.WriteLine(BannerText);
            sw.WriteLine('\f');
            sw.WriteLine(OpticalHeader);
            WritetoOpticalFile(sw, transaction);

            return;
        }

        public static void UpdateCreditCardTotals(Transaction transaction, ref double visatot, ref double CCtotal, ref double visafee, ref double CCFee) 
        {
            if (transaction.Account.Substring(0, 1) == "4")
            {
                visatot += Convert.ToDouble(transaction.PaymentAmount);
            }
            else
            {
                CCtotal += Convert.ToDouble(transaction.PaymentAmount);
            }

            if (transaction.FeeAmount != "")
            {
                if (transaction.Account.Substring(0, 1) == "4")
                {
                    visafee += Convert.ToDouble(transaction.FeeAmount);
                }
                else
                {
                    CCFee += Convert.ToDouble(transaction.FeeAmount);
                }
            }

            return;
        }

        public static bool GetPostedStatus(Transaction transaction) 
        {
            if (transaction.Status.Contains("Posted"))
                return true;
            else if (transaction.Status.Contains("Approved") || transaction.Status.Contains("Settled") || transaction.PaymentType.Contains("Purchase"))
                return true;
            else if (transaction.Status.Contains("Pending") || transaction.Status.Contains("Settled"))
                return true;
            else if ((transaction.Status.Contains("Pending") || transaction.Status.Contains("Settled") || transaction.Status.Contains("Purchase")) && transaction.PaymentType != "")
                return true;

            return false;
        }

        public static void WritetoOpticalFile(StreamWriter sw, Transaction transaction) 
        {
            string OpticalLine = transaction.Account + "       " + transaction.PaymentAmount;
            OpticalLine = PadString(OpticalLine, 49);
            OpticalLine += transaction.FeeAmount;
            OpticalLine = PadString(OpticalLine, 67);
            OpticalLine += transaction.PostDate;
            sw.WriteLine(OpticalLine);
        }

        public static bool GetFieldLocations(string[] fields, ref int accountLoc, ref int dateLoc, 
            ref int paymentAmountLoc, ref int feeAmountLoc, ref int statusLoc, ref int paymentTypeLoc) 
        {
            int x = 0;
            
            while (x < fields.Length) 
            {
                if (fields[x].Contains("Account Number:"))
                    accountLoc = x;
                else if (fields[x].Contains("Convenience Fee:"))
                    feeAmountLoc = x;
                else if (fields[x].Contains("Load Date") || fields[x].Contains("Capture Date") || fields[x].Contains("Tran Date"))
                    dateLoc = x;
                else if (fields[x].Contains("Amount"))
                    paymentAmountLoc = x;
                else if (fields[x].Contains("Status") || fields[x].Contains("Network Response"))
                    statusLoc = x;
                else if (fields[x].Contains("Tran Type"))
                    paymentTypeLoc = x;

                x++;
            }

            return true;
        }
    }
}
