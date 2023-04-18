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
 *
 *Update 04/18/2023 - Tom Strohecker 
 * Added logic to format date when file is saved as a csv and not txt
 * Added logic to exception transaction when it is voided: does not contain (00) as status code.
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
        public static DateTime today = DateTime.Today;
        public static bool Heading;

        //Directory Paths
        public static string SourceDirectory = @"C:\FileTransfers\Incoming_Files\EPX\";//Start folder of file
        public static string ProcessingDirectory = @"C:\FileTransfers\Incoming_Files\EPX\Processing\";//Processing Directory
        public static string ArchiveDirectory = @"C:\FileTransfers\Incoming_Files\EPX\Archive\";//Archive Directory after processing finishes
        public static string[] poschar = { "{", "A", "B", "C", "D", "E", "F", "G", "H", "I" };
        
        //Synergy Files
        public static string datestamp = DateTime.Now.ToString("MMddyyyy hhmm");
        public static string OpticalFile = SourceDirectory + "EPX Credit Card Payments" + datestamp + ".txt";
        public static string OpticalPayment = SourceDirectory + "EPX SHLN Payments" + datestamp + ".txt";
        public static string OpticalHeader = "AccountNumber          Payment Amount           Fee Amount        Date";

        //OLD AND NEW VISA FILE VARIABLES
        public static string OldFileName = SourceDirectory + "oldfile.txt";
        public static string VisaFilename = SourceDirectory + "Visafdr.tap" + ".txt";
        public static string Fname = SourceDirectory + "fdr.tap.txt";
        public static string episysFile = SourceDirectory + "EPX.TRAN.txt";
        public static string ExceptionFile = SourceDirectory + "Exception File_" + today + ".txt";
        public static FieldLocations fieldLocations = new FieldLocations();
        public static Totals totals = new Totals();
        public static string FileName;//file name variable used for creating and changing file
      
        static void Main()
        {                   
            try
            {
                string Line8 = "8403946100055557 ";
                string Line9 = "959961000";
                string NewLine8 = "8514059300055555 ";
                string NewLine9 = "981503000";
                string[] Sfiles = Directory.GetFiles(SourceDirectory);
                MoveFilestoProcessDirectory(Sfiles);
                string[] Pfiles = Directory.GetFiles(ProcessingDirectory);//holds all files moved to processing directory

                foreach (string f in Pfiles)//get the file from new folder
                {
                    if (f.IndexOf("_S") <= 0 && f.IndexOf("RECON") > 0)
                    {
                        FileName = SourceDirectory + "fdr.tap" + ".txt";
                        
                        if (f.IndexOf("ACHRECON") > 0)
                        {
                            ProcessFile(f, "A");
                        }//ACHRECON
                        else if (f.IndexOf("CCRECON") > 0)
                        {
                            ProcessFile(f, "C");
                        }//CCRECON

                        ArchiveFile(f);
                    }
                }

                PrintTotals();
                WriteTap(VisaFilename, NewLine8, NewLine9, totals.VisaAmount, totals.VisaCount);
                WriteTap(OldFileName, Line8, Line9, totals.OldVisaAmount, totals.OldVisaCount);
                MergeFile(OldFileName, VisaFilename, Fname);
            }
            catch (Exception e)
            {
                Console.WriteLine("The process failed: {0}", e.ToString());
            }
        }

        private static void PrintTotals() 
        {
            if (File.Exists(OpticalFile))
            {
                using StreamWriter sw = File.AppendText(OpticalFile);
                sw.WriteLine("\n");
                sw.WriteLine("Total Visa Payment: " + totals.visatot.ToString("0.00"));
                sw.WriteLine("Total Visa Fee: " + totals.visafee.ToString("0.00"));
                sw.WriteLine("Total Visa Pay-Fee: " + (totals.visatot - totals.visafee).ToString("0.00"));
                sw.WriteLine("\n");
                sw.WriteLine("Total Non-Visa Payment: " + totals.CCtotal.ToString("0.00"));
                sw.WriteLine("Total Non-Visa Fee: " + totals.CCFee.ToString("0.00"));
                sw.WriteLine("Total Non-Visa Pay-Fee: " + (totals.CCtotal - totals.CCFee).ToString("0.00"));
            }

            if (File.Exists(OpticalPayment))
            {
                using StreamWriter sw = File.AppendText(OpticalPayment);
                sw.WriteLine("\n");
                sw.WriteLine("Total Payment: " + totals.SLTot.ToString());
                sw.WriteLine("Total Fee: " + totals.SLFee.ToString());
                sw.WriteLine("Total Pay-Fee: " + (totals.SLTot - totals.SLFee).ToString());
            }
        }

        private static void ArchiveFile(string f) 
        {
            String MoveFile = System.IO.Path.GetFileName(f);
            string Destination = System.IO.Path.Combine(ArchiveDirectory, MoveFile);
            Destination = Destination[0..^4] + datestamp + ".txt";
            System.IO.File.Move(f, Destination);
            Console.WriteLine("___ {0} was moved to {1}", System.IO.Path.GetFileNameWithoutExtension(f), ArchiveDirectory);

            return;
        }

        private static void ProcessFile(string f, string fileType) 
        {
            fieldLocations.fieldsMapped = false;

            foreach (var i in File.ReadLines(f))
            {
                string Line = i;
                Heading = WriteHeader(Heading);
                string ParsedLine = RemoveCharacters(Line);
                string[] columns = ParsedLine.Split('\t');
                
                if (fieldLocations.fieldsMapped == false)
                {
                    fieldLocations.fieldsMapped = GetFieldLocations(columns);
                }

                Transaction transaction = PopulateTransaction(columns);
                transaction.FileType = fileType;
                ProcessPayment(transaction, i);
            }

            return;
        }

        private static Transaction PopulateTransaction(string[] columns) 
        {
            Transaction transaction = new Transaction();
            transaction.Account = columns[fieldLocations.accountLoc];
            transaction.FeeAmount = columns[fieldLocations.feeAmountLoc];
            transaction.PaymentAmount = columns[fieldLocations.paymentAmountLoc];
            transaction.Status = columns[fieldLocations.statusLoc];
            transaction.PostDate = columns[fieldLocations.dateLoc];
            transaction.PaymentType = fieldLocations.paymentTypeLoc == -1 ? "" : columns[fieldLocations.paymentTypeLoc];
            return transaction; 
        } 

        private static void MoveFilestoProcessDirectory(string[] Sfiles) 
        {
            foreach (string f in Sfiles)
            {
                string Filename = System.IO.Path.GetFileName(f);
                string Destination = System.IO.Path.Combine(ProcessingDirectory, Filename);
                System.IO.File.Move(f, Destination);
                Console.WriteLine("___ {0} was moved to {1}", System.IO.Path.GetFileNameWithoutExtension(f), ProcessingDirectory);
            }

            return;
        }

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

        private static void WriteTap(string FName, string L8, string L9, double Amt, int C)
        {
            //format the count variable for the trailer lines
            string VCount = Convert.ToString(C);
            int num = Convert.ToInt16(VCount.Substring(VCount.Length - 1, 1));
            VCount = VCount[0..^1] + poschar[num];
            VCount = PadString(VCount, 7, "0", "FRONT");
            string VAmount = Amt.ToString("N2");//Convert.ToString(Amt);
            VAmount = FormatMoneyString(VAmount);
            num = Convert.ToInt16(VAmount.Substring(VAmount.Length - 1, 1));
            VAmount = VAmount[0..^1] + poschar[num];
            VAmount = PadString(VAmount, 9, "0", "FRONT");
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

        public static bool WriteHeader(bool Heading) 
        {
            string Line1 = "159961000" + today.ToString("MMddyy") + "Y1   0001   Y" + "                                                   ";
            string Line2 = "2403946100055557    1                                                          ";
            string NewLine1 = "181503000" + today.ToString("MMddyy") + "Y1   0001   Y" + "                                                   ";
            string NewLine2 = "2514059300055555    1                                                          ";
            
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

        public static void ProcessPayment(Transaction transaction, string i) 
        {
            bool result = double.TryParse(transaction.Account, out _);
            transaction.VisaFlag = false;
            transaction.NewVisaFlag = false;
            string basefee = "10"; //Base fee value
            string feeamt = "10.00"; //Base fee monetary value

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
                    transaction = GetCreditCardType(transaction);
                }

                bool postedTransaction = GetPostedStatus(transaction);

                if (transaction.VisaFlag == true && postedTransaction)
                {
                    PostCCPayment(transaction);
                }
                //File created will need to be placed into Episys so that the transactions can be posted to the member account
                else if (((transaction.Account.Substring(0, 1) == "1" || transaction.Account.Substring(0, 1) == "2") && postedTransaction && transaction.Account.Length == 13) || (transaction.VisaFlag))//create file to be sent to episys as letter file
                {
                    transaction.PaymentAmount = transaction.PaymentAmount.Replace("$", "");
                    transaction.FeeAmount = transaction.FeeAmount.Replace("$", "");

                    if (File.Exists(OpticalPayment))
                    {
                        using StreamWriter sw = File.AppendText(OpticalPayment);
                        totals.SLTot += Convert.ToDouble(transaction.PaymentAmount);

                        if (transaction.FeeAmount != "")
                        {
                            totals.SLFee += Convert.ToDouble(transaction.FeeAmount); 
                        }

                        WritetoOpticalFile(sw, transaction);
                    }
                    else
                    {
                        using StreamWriter sw = new StreamWriter(OpticalPayment, true);
                        string ReportTitle = "EPX Payment Report";
                        string RepCat = "CREDEBCARD";
                        string BannerText = BannerPageCreation(RepCat, ReportTitle);
                        totals.SLTot += Convert.ToDouble(transaction.PaymentAmount);

                        if (transaction.FeeAmount != "")
                        {
                            totals.SLFee += Convert.ToDouble(transaction.FeeAmount);
                        }

                        sw.WriteLine(BannerText);
                        sw.WriteLine('\f');
                        sw.WriteLine(OpticalHeader);
                        WritetoOpticalFile(sw, transaction);
                    }

                    string Line = transaction.Account + "|" + transaction.PostDate + "|" + transaction.PaymentAmount + "|" + transaction.FeeAmount + "|" + transaction.FileType;
                    WriteToFile(episysFile, Line);
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

        public static Transaction GetCreditCardType(Transaction transaction) 
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

        public static void PostCCPayment(Transaction transaction) 
        {
            transaction.PaymentAmount = transaction.PaymentAmount.Replace("$", "");
            transaction.FeeAmount = transaction.FeeAmount.Replace("$", "");

            if (File.Exists(OpticalFile))
            {
                using StreamWriter sw = File.AppendText(OpticalFile);
                UpdateCreditCardTotals(transaction);
                WritetoOpticalFile(sw, transaction);
            }
            else
            {
                CreateOpticalFile(transaction);
            }
            
            transaction.PaymentAmount = FormatMoneyString(transaction.PaymentAmount);
            transaction.FeeAmount = FormatMoneyString(transaction.FeeAmount);
            transaction.PaymentAmount = transaction.PaymentAmount.Replace("$", "");          
            string vma = Convert.ToString(Convert.ToDouble(transaction.PaymentAmount) - Convert.ToDouble(transaction.FeeAmount));
            vma = vma[0..^1];
            int num = Convert.ToInt16(transaction.PaymentAmount.Substring(transaction.PaymentAmount.Length - 1, 1));//trail character for amount                                                                                                                    //lineamount=payment amount - fee amount                                                                                                            //lineamount = Convert.ToString((Convert.ToInt32(columns[9]) - Convert.ToInt32(columns[15]))).Substring(0, Convert.ToString((Convert.ToInt32(columns[9]) - Convert.ToInt32(columns[15]))).Length - 1) + poschar[num];
            string lineamount = vma + poschar[num];
            lineamount = PadString(lineamount, 7, "0", "FRONT");
             //Each data line will be formatted 5 + card number + date + amount + P
            bool isNumeric = long.TryParse(transaction.Account, out _);
            string temp = transaction.Account;

            while (isNumeric == false && temp.Length > 10)
            {
                temp = temp[1..].TrimStart().TrimEnd();
                isNumeric = long.TryParse(temp, out _);
            }
            string dateString = FormatDateString(transaction.PostDate);
            string dataline = "5" + temp + dateString + lineamount + "P";
            string Line = dataline + "                                                ";
            WriteToFile(FileName, Line);
            Line = transaction.Account + "|" + transaction.PostDate + "|" + transaction.PaymentAmount + "|" + transaction.FeeAmount + "|" + transaction.FileType;
            WriteToFile(episysFile, Line);
            return;
        }

        public static string FormatDateString(string postDate) 
        {
            string[] tempstringarray = postDate.Split('/');

            if (tempstringarray.Length > 1) 
            {
                if (tempstringarray[0].Length < 2)
                    tempstringarray[0] = "0" + tempstringarray[0];

                if (tempstringarray[1].Length < 2)
                    tempstringarray[1] = "0" + tempstringarray[1];

                if(tempstringarray[2].Length > 2)
                    tempstringarray[2] = tempstringarray[2].Substring(tempstringarray[2].Length - 2, 2);
                

                return tempstringarray[0] + tempstringarray[1] + tempstringarray[2];
            }

            return "No Date";
        }

        public static String PadString(string inputString, int charLength, string padChar, string padLoc) 
        {
            while (inputString.Length < charLength)
            {
                if (padLoc == "END")
                    inputString += padChar;
                else
                    inputString = padChar + inputString;
            }

            return inputString;
        }

        public static string FormatMoneyString(string amount) 
        {
            if (amount.Contains("."))
                amount = amount.Replace(".", "");           
            else 
                amount += "00";

            amount = amount.Replace(",", "");
            return amount;
        }

        public static void CreateOpticalFile(Transaction transaction) 
        {
            using StreamWriter sw = new StreamWriter(OpticalFile, true);
            string ReportTitle = "EPX Credit Card Report";
            string RepCat = "CREDEBCARD";
            string BannerText = BannerPageCreation(RepCat, ReportTitle);
            UpdateCreditCardTotals(transaction);
            sw.WriteLine(BannerText);
            sw.WriteLine('\f');
            sw.WriteLine(OpticalHeader);
            WritetoOpticalFile(sw, transaction);

            return;
        }

        public static void UpdateCreditCardTotals(Transaction transaction) 
        {
            if (transaction.NewVisaFlag == true)
            {
                totals.CCtotal += Convert.ToDouble(transaction.PaymentAmount);
                totals.VisaAmount += (Convert.ToDouble(transaction.PaymentAmount) - Convert.ToDouble(transaction.FeeAmount));//total amount for trailer lines **subtract the fee amount from the payment line
                totals.VisaCount++;

                if (transaction.FeeAmount != "")
                    totals.CCFee += Convert.ToDouble(transaction.FeeAmount);
            }
            else
            {
                totals.visatot += Convert.ToDouble(transaction.PaymentAmount);
                totals.OldVisaAmount += (Convert.ToDouble(transaction.PaymentAmount) - Convert.ToDouble(transaction.FeeAmount));//total amount for trailer lines **subtract the fee amount from the payment line
                totals.OldVisaCount++;

                if (transaction.FeeAmount != "")
                    totals.visafee += Convert.ToDouble(transaction.FeeAmount);
            }

            return;
        }

        public static bool GetPostedStatus(Transaction transaction) 
        {
            if (transaction.Status.Contains("Posted"))
                return true;
            else if ((transaction.Status.Contains("Approved") || transaction.Status.Contains("Settled")) && transaction.PaymentType.Contains("Purchase"))
                return true;
            else if (transaction.Status.Contains("Pending") || transaction.Status.Contains("Settled") || transaction.Status.Contains("Approved"))
                return true;
            else if ((transaction.Status.Contains("Pending") || transaction.Status.Contains("Settled") || transaction.Status.Contains("Purchase")) && transaction.PaymentType != "")
                return true;
            else if (transaction.Status.Contains("(00)"))
                return true;

            return false;
        }

        public static void WritetoOpticalFile(StreamWriter sw, Transaction transaction) 
        {
            string OpticalLine = transaction.Account + "       " + transaction.PaymentAmount;
            OpticalLine = PadString(OpticalLine, 49, " ", "END");
            OpticalLine += transaction.FeeAmount;
            OpticalLine = PadString(OpticalLine, 67, " ", "END");
            OpticalLine += transaction.PostDate;
            sw.WriteLine(OpticalLine);
        }

        public static bool GetFieldLocations(string[] fields) 
        {
            int x = 0;
            
            while (x < fields.Length) 
            {
                fields[x] = fields[x].Replace("_", " ");

                if (fields[x].Contains("Account Number"))
                    fieldLocations.accountLoc = x;
                else if (fields[x].Contains("Convenience Fee"))
                    fieldLocations.feeAmountLoc = x;
                else if (fields[x].Contains("Load Date") || fields[x].Contains("Capture Date") || fields[x].Contains("Tran Date"))
                    fieldLocations.dateLoc = x;
                else if (fields[x].Contains("Amount"))
                    fieldLocations.paymentAmountLoc = x;
                else if (fields[x].Contains("Status") || fields[x].Contains("Network Response"))
                    fieldLocations.statusLoc = x;
                else if (fields[x].Contains("Tran Type"))
                    fieldLocations.paymentTypeLoc = x;

                x++;
            }

            return true;
        }
    }
}
