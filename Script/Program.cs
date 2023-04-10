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
 * May need logic to parse a string containing the account/card number, s/v/l, and SLId
 * 
 * Two additional reports which will be sent into synergy will be created. One for visa and one 
 * for SL transactions.
 * 
 */

/*
 * Will need  to split the account, SLID, and Share/Loan type from lookup entered
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
 */

//TODO: Add logic to ensure that there is an actual visa payment for the file to be created. 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;



namespace EPX_File_Script
{
    public class TRANS
    {
        public string name;
        public string account;
        public string type;
        public string ID;
        public string date;
        public string amount;
        public string fee;

        public TRANS()
        {
            name = "";
            account = "";
            type = "";
            ID = "";
            date = "";
            amount = "";
            fee = "";
        }
        public TRANS(string a, string b, string c, string d, string e, string f, string g)
        {
            name = a;
            account = b;
            type = c;
            ID = d;
            date = e;
            amount = f;
            fee = g;
        }
        public string Visa()
        {
            string line = "";

            line = "5" + account + date + amount;
            return line;
        }
        public string SL()
        {
            string line = "";
            line = name + "|" + type + "|" + account + "|" + ID + "|" + amount;
            return line;
        }
    }
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

            String BannerText = "";
            String TempText = "";
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
                    NewStr = NewStr + " ";
                }
            }
            else if (Cat == "T")
            {
                for (int i = NumbSp; i < 83; i++)
                {
                    NewStr = NewStr + " ";
                }
            }
            return NewStr;
        }

        static void Main(string[] args)
        {

            //Directory Paths
         //   string SourceDirectory = @"Z:\Projects\In Progress\2019\VISA Script Updates\EPX\Source\";//Start folder of file
         //   string ProcessingDirectory = @"Z:\Projects\In Progress\2019\VISA Script Updates\EPX\Processing\";//Processing Directory
         //   string ArchiveDirectory = @"Z:\Projects\In Progress\2019\VISA Script Updates\EPX\ARCHIVE\";//Archive Directory after processing finishes


            //Directory Paths

            string SourceDirectory = @"C:\FileTransfers\Incoming_Files\EPX\";//Start folder of file
            string ProcessingDirectory = @"C:\FileTransfers\Incoming_Files\EPX\Processing\";//Processing Directory
            string ArchiveDirectory = @"C:\FileTransfers\Incoming_Files\EPX\Archive\";//Archive Directory after processing finishes


            //Variable(s) for formatting the file
            string line = "";   //variable that holds each line of file for modification
            string parsedline = "";

            //When the fee amount is changed the below variables will need adjusted to validate fee amount
            string basefee = "10"; //Base fee value
            string feeamt = "10.00"; //Base fee monetary value


            string[] poschar = { "{", "A", "B", "C", "D", "E", "F", "G", "H", "I" };
            string[] negchar = { "}", "J", "K", "L", "M", "N", "O", "P", "Q", "R" };
            string heading = "false";
            //file variables
            string Filename = "default.txt";//file name variable used for creating and changing file
            string Destination = System.IO.Path.Combine(ProcessingDirectory, Filename);//sets the path of file

            //File Line Variables
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

            int num;

            //string VCount = ""; //string variable of Visa Count
            //string VAmount = "";//String Variable of Visa Amount
            string dataline = "";
            string lineamount = "";

            //Synergy Files
            string datestamp = DateTime.Now.ToString("MMddyyyy hhmm");
            string OpticalFile = SourceDirectory + "EPX Credit Card Payments" + datestamp + ".txt";
            string OpticalPayment = SourceDirectory + "EPX SHLN Payments" + datestamp + ".txt";
            string OpticalHeader = "AccountNumber          Payment Amount           Fee Amount        Date";
            string OpticalLine = "";
            double visatot = 0.00;
            double CCtotal = 0.00;
            double CCFee = 0.00;
            double visafee = 0.00;
            double SLTot = 0.00;
            double SLFee = 0.00;
            //                      1234567890123456789012345678901234567890123456789012345678901234567890
            //                               1         2         3         4         5         6         7

            //variables to validate for duplicates.
            string OCCFile = ArchiveDirectory + "CCRECONtemp" + (DateTime.Today.AddDays(-1)).ToString("MMddyyyy");
            string OACHFile = ArchiveDirectory + "ACHRECONtemp" + (DateTime.Today.AddDays(-1)).ToString("MMddyyyy");
            string[] CCLines = new string[1000];
            string[] ACHLines = new string[1000];


            //OLD AND NEW VISA FILE VARIABLES
            int VisaCount = 0;
            double VisaAmount = 0.00;
            int OldVisaCount = 0;
            double OldVisaAmount = 0.00;

            bool NewVisaFlag = false;
            bool VisaFlag = false;
            string OldFileName = SourceDirectory + "oldfile.txt";
            string VisaFilename = SourceDirectory + "Visafdr.tap" + ".txt";
            string Fname = SourceDirectory + "fdr.tap.txt";
            //bool Found = false;
            //int achcount=0;
            //int cccount=0;

            //START OF FILE PROCESS
            try
            {
                //validate for duplicates
                /*
                string[] OldFiles = Directory.GetFiles(ArchiveDirectory);
                foreach (string f in OldFiles)
                {
                    if (f.Contains((DateTime.Today.AddDays(-1)).ToString("MMddyyyy")))
                    {
                        if (f.Contains("ACHRECON"))
                        {
                            foreach (var i in File.ReadLines(f))
                            {
                                
                                line = i;
                                //remove all double and single quotes from line
                                if (line.Contains("\"")) { line = line.Replace("\"" + "," + "\"", "\t"); }
                                if (line.Contains("\"")) { line = line.Replace("\"", ""); }
                                if (line.Contains("'")) { line = line.Replace("'", ""); }
                                parsedline = line;

                                //Console.WriteLine(parsedline);

                                //split string by tab                        
                                string[] columns = parsedline.Split('\t');
                                if (columns.Length > 16)
                                {
                                    ACHLines[achcount] = columns[16] + ',' + columns[8] + ',' + columns[15] + columns[11]+columns[2];
                                    achcount++;
                                }
                                else
                                {
                                    ACHLines[achcount] = columns[14] + ',' + columns[8] + ',' + columns[13] + columns[10] + columns[2];
                                    achcount++;
                                }
                            }
                        }
                        else if (f.Contains("CCRECON"))
                        {
                            foreach (var i in File.ReadLines(f))
                            {
                                line = i;
                                //remove all double and single quotes from line
                                if (line.Contains("\"")) { line = line.Replace("\"" + "," + "\"", "\t"); }
                                if (line.Contains("\"")) { line = line.Replace("\"", ""); }
                                if (line.Contains("'")) { line = line.Replace("'", ""); }
                                parsedline = line;

                                //Console.WriteLine(parsedline);

                                //split string by tab                        
                                string[] columns = parsedline.Split('\t');
                                if (columns.Length >= 17)
                                {
                                    CCLines[cccount] = columns[17] + ',' + columns[5] + ',' + columns[16] + columns[10] + columns[1];
                                    cccount++;
                                }
                                else
                                {
                                    CCLines[cccount] = columns[14] + ',' + columns[5] + ',' + columns[13] + columns[8] + columns[3];
                                    cccount++;
                                }

                            }
                        }
                    }
                }//end remove duplicates.
                
                */


                //get the file from folder
                string[] Sfiles = Directory.GetFiles(SourceDirectory);
                string testfile = SourceDirectory + "EPX.TRAN.txt";
                string ExceptionFile = SourceDirectory + "Exception File_" + today + ".txt";
                foreach (string f in Sfiles)
                {

                    //move the original file into folder to be processed
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

                        //IF FILES DIFER WILL NEED TO ADD LOGIC HERE TO PULL THE DATA FROM EACH FILE

                        //name of file sent to PSCU
                        Filename = SourceDirectory + "fdr.tap" + ".txt";

                        //achfile
                        if (f.IndexOf("ACHRECON") > 0)//achrecon
                        {

                            //process each line of the file
                            foreach (var i in File.ReadLines(f))
                            {
                                dataline = "";
                                lineamount = "";
                                line = i;
                                if (heading == "false")
                                {
                                    //create new file and insert lines 1 and lines 2
                                    Filename = OldFileName;
                                    WriteToFile(Filename, Line1);
                                    WriteToFile(Filename, Line2);

                                    Filename = VisaFilename;
                                    WriteToFile(Filename, NewLine1);
                                    WriteToFile(Filename, NewLine2);

                                    heading = "true";//set flag to true so that the headers will only be entered once
                                }
                                //remove all double and single quotes from line
                                if (line.Contains("\"")) { line = line.Replace("\"" + "," + "\"", "\t"); }
                                if (line.Contains("\"")) { line = line.Replace("\"", ""); }
                                if (line.Contains("'")) { line = line.Replace("'", ""); }
                                parsedline = line;


                                //Console.WriteLine(parsedline);

                                //split string by tab                        
                                string[] columns = parsedline.Split('\t');

                                if (columns.Length > 16)
                                {
                                    double tempnum = 0;
                                    bool result = double.TryParse(columns[16], out tempnum);
                                    if (columns[16] != "" && result == true)
                                    {
                                        //CheckDec
                                        if (columns[15].Contains(basefee))
                                        {
                                            columns[15] = feeamt;

                                        }
                                        else
                                        {
                                            columns[15] = "0.00";
                                        }//end Dec
                                        VisaFlag = false;
                                        if ((columns[16].Substring(0, 1) == "4" || columns[16].Substring(0, 1) == "5") && columns[16].Length == 16)
                                        {
                                            VisaFlag = true;
                                            if (columns[16].Substring(0, 1) == "5")
                                            {
                                                NewVisaFlag = true;
                                                Filename = VisaFilename;
                                            }
                                            else
                                            {
                                                NewVisaFlag = false;
                                                Filename = OldFileName;
                                            }
                                        }
                                        //checks to ensure line is a visa payment
                                        //add logic that will validate the length of the card to ensure it is 16 characters long
                                        if (VisaFlag == true && (columns[11].Contains("Posted")))
                                        {
                                            columns[9] = columns[9].Replace("$", "");
                                            columns[15] = columns[15].Replace("$", "");

                                            if (File.Exists(OpticalFile))
                                            {
                                                using (StreamWriter sw = File.AppendText(OpticalFile))
                                                {
                                                    if (columns[16].Substring(0, 1) == "4")
                                                    {
                                                        visatot = visatot + Convert.ToDouble(columns[9]);
                                                    }                                                    
                                                    else
                                                    {
                                                        CCtotal=CCtotal+ Convert.ToDouble(columns[9]);
                                                    }
                                                    if (columns[15] != "")
                                                    {
                                                        if (columns[16].Substring(0, 1) == "4")
                                                        {
                                                            visafee = visafee + Convert.ToDouble(columns[15]);
                                                        }
                                                        else
                                                        {
                                                            CCFee = CCFee + Convert.ToDouble(columns[15]);
                                                        }
                                                        
                                                    }

                                                    OpticalLine = columns[16] + "       " + columns[9];
                                                    while (OpticalLine.Length < 49)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[15];
                                                    while (OpticalLine.Length < 67)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[6];
                                                    sw.WriteLine(OpticalLine);
                                                }
                                            }
                                            else
                                            {
                                                using (StreamWriter sw = new StreamWriter(OpticalFile, true))
                                                {
                                                    string ReportTitle = "EPX Credit Card Report";
                                                    string RepCat = "CREDEBCARD";
                                                    string BannerText = BannerPageCreation(RepCat, ReportTitle);
                                                    if (columns[16].Substring(0, 1) == "4")
                                                    {
                                                        visatot = visatot + Convert.ToDouble(columns[9]);
                                                    }
                                                    else
                                                    {
                                                        CCtotal = CCtotal + Convert.ToDouble(columns[9]);
                                                    }
                                                    if (columns[15] != "")
                                                    {
                                                        if (columns[16].Substring(0, 1) == "4")
                                                        {
                                                            visafee = visafee + Convert.ToDouble(columns[15]);
                                                        }
                                                        else
                                                        {
                                                            CCFee = CCFee + Convert.ToDouble(columns[15]);
                                                        }

                                                    }
                                                    sw.WriteLine(BannerText);
                                                    sw.WriteLine('\f');
                                                    sw.WriteLine(OpticalHeader);

                                                    OpticalLine = columns[16] + "       " + columns[9];
                                                    while (OpticalLine.Length < 49)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[15];
                                                    while (OpticalLine.Length < 67)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[6];
                                                    sw.WriteLine(OpticalLine);
                                                }
                                            }
                                            //remove period from dollar amounts, if there is no period add 00 to the string to indicate change amount
                                            if (columns[9].Contains("."))
                                            {
                                                columns[9] = columns[9].Replace(".", "");
                                            }
                                            else { columns[9] = columns[9] + "00"; }
                                            if (columns[15].Contains("."))
                                            {
                                                columns[15] = columns[15].Replace(".", "");
                                            }
                                            else { columns[15] = columns[15] + "00"; }
                                            columns[9] = columns[9].Replace("$", "");
                                            Console.WriteLine("line 530 testinst");
                                            if (NewVisaFlag == true)
                                            {
                                                VisaAmount = VisaAmount + (Convert.ToDouble(columns[9]) - Convert.ToDouble(columns[15]));//total amount for trailer lines **subtract the fee amount from the payment line
                                            }
                                            else
                                            {
                                                OldVisaAmount = OldVisaAmount + (Convert.ToDouble(columns[9]) - Convert.ToDouble(columns[15]));//total amount for trailer lines **subtract the fee amount from the payment line
                                            }

                                            string vma = Convert.ToString(Convert.ToDouble(columns[9]) - Convert.ToDouble(columns[15]));
                                            vma = vma.Substring(0, vma.Length - 1);

                                            num = Convert.ToInt16(columns[9].Substring(columns[9].Length - 1, 1));//trail character for amount
                                                                                                                  //lineamount=payment amount - fee amount                                                                                                            //lineamount = Convert.ToString((Convert.ToInt32(columns[9]) - Convert.ToInt32(columns[15]))).Substring(0, Convert.ToString((Convert.ToInt32(columns[9]) - Convert.ToInt32(columns[15]))).Length - 1) + poschar[num];
                                            lineamount = vma + poschar[num];

                                            //Add filler 0's to line amount
                                            while (lineamount.Length < 7) { lineamount = "0" + lineamount; }
                                            //Each data line will be formatted 5 + card number + date + amount + P
                                            long z;
                                            bool isNumeric = long.TryParse(columns[16], out z);
                                            string temp = columns[16];
                                            while (isNumeric == false && temp.Length > 10)
                                            {
                                                temp = temp.Substring(1, temp.Length - 1).TrimStart().TrimEnd();
                                                isNumeric = long.TryParse(temp, out z);
                                            }
                                            Console.WriteLine(temp);

                                            dataline = "5" + /*columns[7]*/temp + columns[6].Substring(0, 2) + columns[6].Substring(3, 2) + columns[6].Substring(columns[6].Length - 2, 2) + lineamount + "P";
                                            //Console.WriteLine(dataline);
                                            line = dataline + "                                                ";
                                            if (NewVisaFlag == true) { VisaCount++; }
                                            else { OldVisaCount++; }
                                            WriteToFile(Filename, line);


                                            //new code
                                            line = columns[16] + "|" + columns[6] + "|" + columns[9] + "|" + columns[15] + "|A" /*+ "|" + columns[22] + "|" + columns[18] + "|" + columns[11]*/;
                                            WriteToFile(testfile, line);

                                        }
                                        //File created will need to be placed into Episys so that the transactions can be posted to the member account
                                        else if (((columns[16].Substring(0, 1) == "1" || columns[16].Substring(0, 1) == "2") && (columns[11].Contains("Posted")) && columns[16].Length == 13) || (VisaFlag))//create file to be sent to episys as letter file
                                        {
                                            columns[9] = columns[9].Replace("$", "");
                                            columns[13] = columns[15].Replace("$", "");
                                            /*Here
                                             * 
                                             */
                                            if (File.Exists(OpticalPayment))
                                            {
                                                using (StreamWriter sw = File.AppendText(OpticalPayment))
                                                {
                                                    SLTot = SLTot + Convert.ToDouble(columns[9]);
                                                    if (columns[13] != "")
                                                    {
                                                        SLFee = SLFee + Convert.ToDouble(columns[13]);
                                                    }

                                                    OpticalLine = columns[16] + "       " + columns[9];
                                                    while (OpticalLine.Length < 49)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[13];
                                                    while (OpticalLine.Length < 67)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[1];
                                                    sw.WriteLine(OpticalLine);
                                                }
                                            }
                                            else
                                            {
                                                using (StreamWriter sw = new StreamWriter(OpticalPayment, true))
                                                {
                                                    string ReportTitle = "EPX Payment Report";
                                                    string RepCat = "CREDEBCARD";
                                                    string BannerText = BannerPageCreation(RepCat, ReportTitle);

                                                    SLTot = SLTot + Convert.ToDouble(columns[9]);
                                                    if (columns[13] != "")
                                                    {
                                                        SLFee = SLFee + Convert.ToDouble(columns[13]);
                                                    }

                                                    sw.WriteLine(BannerText);
                                                    sw.WriteLine('\f');
                                                    sw.WriteLine(OpticalHeader);

                                                    OpticalLine = columns[16] + "       " + columns[9];
                                                    while (OpticalLine.Length < 49)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[13];
                                                    while (OpticalLine.Length < 67)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[1];
                                                    sw.WriteLine(OpticalLine);
                                                }
                                            }
                                            //end
                                            line = columns[16] + "|" + columns[6] + "|" + columns[9] + "|" + columns[15] + "|A" /*+ "|" + columns[22] + "|" + columns[18] + "|" + columns[11]*/;
                                            WriteToFile(testfile, line);

                                        }
                                        else//create exception file 
                                        {
                                            ExceptionFile = SourceDirectory + "exception.txt";
                                            WriteToFile(ExceptionFile, i);

                                        }
                                    }//check null account number
                                    else
                                    {
                                        ExceptionFile = SourceDirectory + "exception.txt";
                                        WriteToFile(ExceptionFile, i);

                                    }
                                }//largeACHFile
                                //bad batch output file
                                else if (columns.Length == 15)
                                {
                                    double tempnum = 0;
                                    bool result = double.TryParse(columns[13], out tempnum);
                                    if (columns[13] != "" && result == true)                                  
                                    {
                                        columns[6] = columns[6].Replace("$", "");
                                        columns[12] = columns[12].Replace("$", "");
                                        //CheckDec
                                        if (columns[12].Contains(basefee))
                                        {
                                            columns[12] = feeamt;
                                        }
                                        else
                                        {
                                            columns[12] = "0.00";
                                        }//end Dec
                                        VisaFlag = false;
                                        Console.WriteLine(VisaFlag);
                                        if ((columns[13].Substring(0, 1) == "4" || columns[13].Substring(0, 1) == "5") && columns[13].Length == 16)
                                        {
                                            VisaFlag = true;
                                            if (columns[13].Substring(0, 1) == "5")
                                            {
                                                NewVisaFlag = true;
                                                Filename = VisaFilename;
                                            }
                                            else
                                            {
                                                NewVisaFlag = false;
                                                Filename = OldFileName;
                                            }
                                       
                                        }
                                        Console.WriteLine(VisaFlag);
                                        if (VisaFlag == true && (columns[8].Contains("Approved") || columns[8].Contains("Settled") || columns[4].Contains("Purchase")))
                                        {
                                            if (File.Exists(OpticalFile))
                                            {
                                                using (StreamWriter sw = File.AppendText(OpticalFile))
                                                {
                                                    if (columns[13].Substring(0, 1) == "4")
                                                    {
                                                        visatot = visatot + Convert.ToDouble(columns[6]);
                                                    }
                                                    else
                                                    {
                                                        CCtotal = CCtotal + Convert.ToDouble(columns[6]);
                                                    }
                                                    if (columns[12] != "")
                                                    {
                                                        if (columns[13].Substring(0, 1) == "4")
                                                        {
                                                            visafee = visafee + Convert.ToDouble(columns[12]);
                                                        }
                                                        else
                                                        {
                                                            CCFee = CCFee + Convert.ToDouble(columns[12]);
                                                        }

                                                    }
                                                    OpticalLine = columns[13] + "       " + columns[6];
                                                    while (OpticalLine.Length < 49)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[12];
                                                    while (OpticalLine.Length < 67)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[1];
                                                    sw.WriteLine(OpticalLine);
                                                }
                                            }
                                            else
                                            {
                                                using (StreamWriter sw = new StreamWriter(OpticalFile, true))
                                                {
                                                    string ReportTitle = "EPX Credit Card Report";
                                                    string RepCat = "CREDEBCARD";
                                                    string BannerText = BannerPageCreation(RepCat, ReportTitle);
                                                    if (columns[13].Substring(0, 1) == "4")
                                                    {
                                                        visatot = visatot + Convert.ToDouble(columns[6]);
                                                    }
                                                    else
                                                    {
                                                        CCtotal = CCtotal + Convert.ToDouble(columns[6]);
                                                    }
                                                    if (columns[12] != "")
                                                    {
                                                        if (columns[13].Substring(0, 1) == "4")
                                                        {
                                                            visafee = visafee + Convert.ToDouble(columns[12]);
                                                        }
                                                        else
                                                        {
                                                            CCFee = CCFee + Convert.ToDouble(columns[12]);
                                                        }

                                                    }

                                                    sw.WriteLine(BannerText);
                                                    sw.WriteLine('\f');
                                                    sw.WriteLine(OpticalHeader);

                                                    OpticalLine = columns[13] + "       " + columns[6];
                                                    while (OpticalLine.Length < 49)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[12];
                                                    while (OpticalLine.Length < 67)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[1];
                                                    sw.WriteLine(OpticalLine);
                                                }
                                            }

                                            //remove period from dollar amounts, if there is no period add 00 to the string to indicate change amount
                                            if (columns[6].Contains("."))
                                            {
                                                columns[6] = columns[6].Replace(".", "");
                                            }
                                            else { columns[6] = columns[6] + "00"; }
                                            if (columns[12].Contains("."))
                                            {
                                                columns[12] = columns[12].Replace(".", "");
                                            }
                                            else { columns[12] = columns[12] + "00"; }
                                            columns[6] = columns[6].Replace("$", "");

                                            Console.WriteLine("line 806 testinst");
                                            if (NewVisaFlag == true)
                                            {
                                                VisaAmount = VisaAmount + (Convert.ToDouble(columns[6]) - Convert.ToDouble(columns[12]));//total amount for trailer lines **subtract the fee amount from the payment line
                                            }
                                            else
                                            {
                                                OldVisaAmount = OldVisaAmount + (Convert.ToDouble(columns[6]) - Convert.ToDouble(columns[12]));//total amount for trailer lines **subtract the fee amount from the payment line
                                            }
                                            string vma = Convert.ToString(Convert.ToDouble(columns[6]) - Convert.ToDouble(columns[12]));
                                            vma = vma.Substring(0, vma.Length - 1);
                                            num = Convert.ToInt16(columns[6].Substring(columns[6].Length - 1, 1));//trail character for amount
                                                                                                                  //lineamount=payment amount - fee amount                                                                                                             //lineamount = Convert.ToString((Convert.ToInt32(columns[5]) - Convert.ToInt32(columns[5]))).Substring(0, Convert.ToString((Convert.ToInt32(columns[5]) - Convert.ToInt32(columns[16]))).Length - 1) + poschar[num];
                                            lineamount = vma + poschar[num];

                                            //Add filler 0's to line amount
                                            while (lineamount.Length < 7) { lineamount = "0" + lineamount; }

                                            long z;
                                            bool isNumeric = long.TryParse(columns[13], out z);
                                            string temp = columns[13];
                                            while (isNumeric == false && temp.Length > 10)
                                            {
                                                temp = temp.Substring(1, temp.Length - 1).TrimStart().TrimEnd();
                                                isNumeric = long.TryParse(temp, out z);
                                            }
                                            Console.WriteLine(temp);
                                            //Each data line will be formatted 5 + card number + date + amount + P
                                            dataline = "5" + temp + columns[1].Substring(0, 2) + columns[1].Substring(3, 2) + columns[1].Substring(columns[1].Length - 2, 2) + lineamount + "P";
                                            line = dataline + "                                                ";
                                            if (NewVisaFlag == true) { VisaCount++; }
                                            else { OldVisaCount++; }

                                            //Console.WriteLine("Test-this card is not a card {0}-{1}", columns[13],VisaFlag);
                                            WriteToFile(Filename, line);

                                            line = columns[13] + "|" + columns[1] + "|" + columns[6] + "|" + columns[12] + "|A" /*+ "|" + columns[22] + "|" + columns[18] + "|" + columns[11]*/;
                                            WriteToFile(testfile, line);

                                        }
                                        //add logic to validate that the card number is 16 characters to ensure there is no error with the .tap file
                                        else if (((columns[13].Substring(0, 1) == "1" || columns[13].Substring(0, 1) == "2") && (columns[8].Contains("Approved") || columns[8].Contains("Settled") || columns[8].Contains("Purchase")) && columns[13].Length == 13) || (VisaFlag == true && (columns[8].Contains("Approved") || columns[8].Contains("Settled") || columns[4].Contains("Purchase"))))//create file to be sent to episys as letter file
                                        {

                                            if (File.Exists(OpticalPayment))
                                            {
                                                using (StreamWriter sw = File.AppendText(OpticalPayment))
                                                {
                                                    columns[6] = columns[6].Replace("$", "");
                                                    columns[12] = columns[12].Replace("$", "");
                                                    SLTot = SLTot + Convert.ToDouble(columns[6]);
                                                    if (columns[12] != "") { SLFee = SLFee + Convert.ToDouble(columns[12]); }

                                                    OpticalLine = columns[13] + "       " + columns[6];
                                                    while (OpticalLine.Length < 49)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[12];
                                                    while (OpticalLine.Length < 67)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[1];
                                                    sw.WriteLine(OpticalLine);
                                                }
                                            }
                                            else
                                            {
                                                using (StreamWriter sw = new StreamWriter(OpticalPayment, true))
                                                {
                                                    columns[6] = columns[6].Replace("$", "");
                                                    columns[12] = columns[12].Replace("$", "");
                                                    string ReportTitle = "EPX Payment Report";
                                                    string RepCat = "CREDEBCARD";
                                                    string BannerText = BannerPageCreation(RepCat, ReportTitle);
                                                    SLTot = SLTot + Convert.ToDouble(columns[6]);
                                                    if (columns[12] != "") { SLFee = SLFee + Convert.ToDouble(columns[12]); }

                                                    sw.WriteLine(BannerText);
                                                    sw.WriteLine('\f');
                                                    sw.WriteLine(OpticalHeader);

                                                    OpticalLine = columns[13] + "       " + columns[6];
                                                    while (OpticalLine.Length < 49)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[12];
                                                    while (OpticalLine.Length < 67)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[1];
                                                    sw.WriteLine(OpticalLine);
                                                }
                                            }


                                            line = columns[13] + "|" + columns[1] + "|" + columns[6] + "|" + columns[12] + "|A" /*+ "|" + columns[22] + "|" + columns[18] + "|" + columns[11]*/;

                                            WriteToFile(testfile, line);

                                        }
                                        else//create exception file 
                                        {
                                            if (columns[13].TrimEnd().TrimStart() != "")
                                            {
                                                ExceptionFile = SourceDirectory + "exception.txt";
                                                WriteToFile(ExceptionFile, i);

                                            }

                                        }
                                    }
                                    else
                                    {
                                        ExceptionFile = SourceDirectory + "exception.txt";
                                        WriteToFile(ExceptionFile, i);
                                    }
                                }
                                //end bad batch 
                                else//short file ach
                                {
                                    double tempnum = 0;
                                    bool result = double.TryParse(columns[14], out tempnum);

                                    Console.WriteLine(result);
                                    Console.WriteLine(columns[14]);

                                    if (columns[14] != "" && result == true)
                                    {
                                        columns[8] = columns[8].Replace("$", "");
                                        columns[13] = columns[13].Replace("$", "");
                                        //CheckDec
                                        if (columns[13].Contains(basefee))
                                        {
                                            columns[13] = feeamt;
                                        }
                                        else
                                        {
                                            columns[13] = "0.00";
                                        }//end Dec
                                         //validate that the card number is at least 16 characters long to ensure that the .tap file does not error
                                        VisaFlag = false;
                                        if ((columns[14].Substring(0, 1) == "4" || columns[14].Substring(0, 1) == "5") && columns[14].Length == 16)
                                        {
                                            VisaFlag = true;
                                            if (columns[14].Substring(0, 1) == "5")
                                            {
                                                NewVisaFlag = true;
                                                Filename = VisaFilename;
                                            }
                                            else
                                            {
                                                NewVisaFlag = false;
                                                Filename = OldFileName;
                                            }
                                        }
                                        if (VisaFlag && (columns[10].Contains("Approved") || columns[10].Contains("Settled") || columns[4].Contains("Purchase")))
                                        {
                                            if (File.Exists(OpticalFile))
                                            {
                                                using (StreamWriter sw = File.AppendText(OpticalFile))
                                                {
                                                    
                                                    if (columns[14].Substring(0, 1) == "4")
                                                    {
                                                        visatot = visatot + Convert.ToDouble(columns[8]);
                                                        Console.WriteLine("this card={0}", columns[14]);
                                                    }
                                                    else
                                                    {
                                                        CCtotal = CCtotal + Convert.ToDouble(columns[8]);
                                                        Console.WriteLine("that card={0}", columns[14]);
                                                    }
                                                    if (columns[13] != "")
                                                    {
                                                        if (columns[14].Substring(0, 1) == "4")
                                                        {
                                                            visafee = visafee + Convert.ToDouble(columns[13]);
                                                        }
                                                        else
                                                        {
                                                            CCFee = CCFee + Convert.ToDouble(columns[13]);
                                                        }

                                                    }

                                                    OpticalLine = columns[14] + "       " + columns[8];
                                                    while (OpticalLine.Length < 49)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[13];
                                                    while (OpticalLine.Length < 67)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[1];
                                                    sw.WriteLine(OpticalLine);
                                                }
                                            }
                                            else
                                            {
                                                using (StreamWriter sw = new StreamWriter(OpticalFile, true))
                                                {
                                                    string ReportTitle = "EPX Credit Card Report";
                                                    string RepCat = "CREDEBCARD";
                                                    string BannerText = BannerPageCreation(RepCat, ReportTitle);
                                                    //fixithere
                                                  
                                                    if (columns[14].Substring(0, 1) == "4")
                                                    {
                                                        visatot = visatot + Convert.ToDouble(columns[8]);
                                                        Console.WriteLine("this card={0}", columns[14]);
                                                    }
                                                    else
                                                    {
                                                        CCtotal = CCtotal + Convert.ToDouble(columns[8]);
                                                        Console.WriteLine("that card={0}", columns[14]);
                                                    }
                                                    if (columns[13] != "")
                                                    {
                                                        if (columns[14].Substring(0, 1) == "4")
                                                        {
                                                            visafee = visafee + Convert.ToDouble(columns[13]);
                                                        }
                                                        else
                                                        {
                                                            CCFee = CCFee + Convert.ToDouble(columns[13]);
                                                        }

                                                    }


                                                    sw.WriteLine(BannerText);
                                                    sw.WriteLine('\f');
                                                    sw.WriteLine(OpticalHeader);

                                                    OpticalLine = columns[14] + "       " + columns[8];
                                                    while (OpticalLine.Length < 49)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[13];
                                                    while (OpticalLine.Length < 67)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[1];
                                                    sw.WriteLine(OpticalLine);
                                                }
                                            }
                                            //remove period from dollar amounts, if there is no period add 00 to the string to indicate change amount
                                            if (columns[8].Contains("."))
                                            {
                                                columns[8] = columns[8].Replace(".", "");
                                            }
                                            else { columns[8] = columns[8] + "00"; }
                                            if (columns[13].Contains("."))
                                            {
                                                columns[13] = columns[13].Replace(".", "");
                                            }
                                            else { columns[13] = columns[13] + "00"; }
                                            columns[8] = columns[8].Replace("$", "");

                                            Console.WriteLine("line 1012 testinst");
                                            if (NewVisaFlag == true)
                                            {
                                                VisaAmount = VisaAmount + (Convert.ToDouble(columns[8]) - Convert.ToDouble(columns[13]));//total amount for trailer lines **subtract the fee amount from the payment line
                                            }
                                            else
                                            {
                                                OldVisaAmount = OldVisaAmount + (Convert.ToDouble(columns[8]) - Convert.ToDouble(columns[13]));//total amount for trailer lines **subtract the fee amount from the payment line
                                            }

                                            string vma = Convert.ToString(Convert.ToDouble(columns[8]) - Convert.ToDouble(columns[13]));
                                            vma = vma.Substring(0, vma.Length - 1);
                                            num = Convert.ToInt16(columns[8].Substring(columns[8].Length - 1, 1));//trail character for amount
                                                                                                                  //lineamount=payment amount - fee amount                                                                                                             //lineamount = Convert.ToString((Convert.ToInt32(columns[5]) - Convert.ToInt32(columns[5]))).Substring(0, Convert.ToString((Convert.ToInt32(columns[5]) - Convert.ToInt32(columns[16]))).Length - 1) + poschar[num];
                                            lineamount = vma + poschar[num];

                                            //Add filler 0's to line amount
                                            while (lineamount.Length < 7) { lineamount = "0" + lineamount; }

                                            long z;
                                            bool isNumeric = long.TryParse(columns[14], out z);
                                            string temp = columns[14];
                                            while (isNumeric == false && temp.Length > 10)
                                            {
                                                temp = temp.Substring(1, temp.Length - 1).TrimStart().TrimEnd();
                                                isNumeric = long.TryParse(temp, out z);
                                            }
                                            Console.WriteLine(temp);
                                            //Each data line will be formatted 5 + card number + date + amount + P
                                            dataline = "5" + temp + columns[1].Substring(0, 2) + columns[1].Substring(3, 2) + columns[1].Substring(columns[1].Length - 2, 2) + lineamount + "P";
                                            line = dataline + "                                                ";
                                            if (NewVisaFlag == true) { VisaCount++; }
                                            else { OldVisaCount++; }
                                            WriteToFile(Filename, line);

                                            line = columns[14] + "|" + columns[1] + "|" + columns[8] + "|" + columns[13] + "|A" /*+ "|" + columns[22] + "|" + columns[18] + "|" + columns[11]*/;
                                            WriteToFile(testfile, line);

                                        }
                                        //if the transaction is a card validate to ensure that the card number is at least 16 characters long 
                                        else if (((columns[14].Substring(0, 1) == "1" || columns[14].Substring(0, 1) == "2") && (columns[10].Contains("Approved") || columns[10].Contains("Settled") || columns[10].Contains("Purchase")) && columns[14].Length == 13) || (VisaFlag == true && (columns[10].Contains("Approved") || columns[10].Contains("Settled") || columns[4].Contains("Purchase"))))//create file to be sent to episys as letter file
                                        {

                                            if (File.Exists(OpticalPayment))
                                            {
                                                using (StreamWriter sw = File.AppendText(OpticalPayment))
                                                {
                                                    columns[8] = columns[8].Replace("$", "");
                                                    columns[13] = columns[13].Replace("$", "");
                                                    SLTot = SLTot + Convert.ToDouble(columns[8]);
                                                    if (columns[13] != "") { SLFee = SLFee + Convert.ToDouble(columns[13]); }

                                                    OpticalLine = columns[14] + "       " + columns[8];
                                                    while (OpticalLine.Length < 49)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[13];
                                                    while (OpticalLine.Length < 67)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[1];
                                                    sw.WriteLine(OpticalLine);
                                                }
                                            }
                                            else
                                            {
                                                using (StreamWriter sw = new StreamWriter(OpticalPayment, true))
                                                {
                                                    columns[8] = columns[8].Replace("$", "");
                                                    columns[13] = columns[13].Replace("$", "");
                                                    string ReportTitle = "EPX Payment Report";
                                                    string RepCat = "CREDEBCARD";
                                                    string BannerText = BannerPageCreation(RepCat, ReportTitle);
                                                    SLTot = SLTot + Convert.ToDouble(columns[8]);
                                                    if (columns[13] != "") { SLFee = SLFee + Convert.ToDouble(columns[13]); }

                                                    sw.WriteLine(BannerText);
                                                    sw.WriteLine('\f');
                                                    sw.WriteLine(OpticalHeader);

                                                    OpticalLine = columns[14] + "       " + columns[8];
                                                    while (OpticalLine.Length < 49)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[13];
                                                    while (OpticalLine.Length < 67)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[1];
                                                    sw.WriteLine(OpticalLine);
                                                }
                                            }
                                            line = columns[14] + "|" + columns[1] + "|" + columns[8] + "|" + columns[13] + "|A" /*+ "|" + columns[22] + "|" + columns[18] + "|" + columns[11]*/;
                                            WriteToFile(testfile, line);
                                        }
                                        else//create exception file 
                                        {
                                            if (columns[14].TrimEnd().TrimStart() != "")
                                            {
                                                ExceptionFile = SourceDirectory + "exception.txt";
                                                WriteToFile(ExceptionFile, i);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        ExceptionFile = SourceDirectory + "exception.txt";
                                        WriteToFile(ExceptionFile, i);
                                    }
                                }//ACHshortfile
                            }
                        }//ACHRECON
                        //cc file, these two files may end up having the same layout for sameday
                        else if (f.IndexOf("CCRECON") > 0)
                        {
                            //process each line of the file
                            foreach (var i in File.ReadLines(f))
                            {
                                dataline = "";
                                lineamount = "";
                                line = i;

                                if (heading == "false")
                                {
                                    //create new file and insert lines 1 and lines 2
                                    Filename = OldFileName;
                                    WriteToFile(Filename, Line1);
                                    WriteToFile(Filename, Line2);

                                    Filename = VisaFilename;
                                    WriteToFile(Filename, NewLine1);
                                    WriteToFile(Filename, NewLine2);

                                    heading = "true";//set flag to true so that the headers will only be entered once
                                }



                                //remove all double and single quotes from line
                                if (line.Contains("\"")) { line = line.Replace("\"" + "," + "\"", "\t"); }
                                if (line.Contains("\"")) { line = line.Replace("\"", ""); }
                                if (line.Contains("'")) { line = line.Replace("'", ""); }
                                parsedline = line;

                                //split string by tab                        
                                string[] columns = parsedline.Split('\t');


                                //checks to ensure line is a visa payment
                               
                                if (columns.Length >= 17)
                                {
                                    double tempnum = 0;
                                    bool result = double.TryParse(columns[17], out tempnum);
                                    if (columns[17] != "" && result == true)
                                    {
                                        columns[5] = columns[5].Replace("$", "");
                                        columns[16] = columns[16].Replace("$", "");
                                        //CheckDec
                                        if (columns[16].Contains(basefee))
                                        {
                                            columns[16] = feeamt;
                                        }
                                        else
                                        {
                                            columns[16] = "0.00";
                                        }//end Dec
                                        VisaFlag = false;
                                        if ((columns[17].Substring(0, 1) == "4" || columns[17].Substring(0, 1) == "5") && columns[17].Length == 16)
                                        {
                                            VisaFlag = true;
                                            if (columns[17].Substring(0, 1) == "5")
                                            {
                                                NewVisaFlag = true;
                                                Filename = VisaFilename;
                                            }
                                            else
                                            {
                                                NewVisaFlag = false;
                                                Filename = OldFileName;
                                            }
                                        }
                                        if (VisaFlag == true && (columns[10].Contains("Pending") || columns[10].Contains("Settled")))
                                        {
                                            if (File.Exists(OpticalFile))
                                            {
                                                using (StreamWriter sw = File.AppendText(OpticalFile))
                                                {
                                                    if (columns[17].Substring(0, 1) == "4")
                                                    {
                                                        visatot = visatot + Convert.ToDouble(columns[5]);
                                                    }
                                                    else
                                                    {
                                                        CCtotal = CCtotal + Convert.ToDouble(columns[5]);
                                                    }
                                                    if (columns[16] != "")
                                                    {
                                                        if (columns[17].Substring(0, 1) == "4")
                                                        {
                                                            visafee = visafee + Convert.ToDouble(columns[16]);
                                                        }
                                                        else
                                                        {
                                                            CCFee = CCFee + Convert.ToDouble(columns[16]);
                                                        }

                                                    }


                                                    OpticalLine = columns[17] + "       " + columns[5];
                                                    while (OpticalLine.Length < 49)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[16];
                                                    while (OpticalLine.Length < 67)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[3];
                                                    sw.WriteLine(OpticalLine);
                                                }
                                            }
                                            else
                                            {
                                                using (StreamWriter sw = new StreamWriter(OpticalFile, true))
                                                {
                                                    string ReportTitle = "EPX Credit Card Report";
                                                    string RepCat = "CREDEBCARD";
                                                    string BannerText = BannerPageCreation(RepCat, ReportTitle);
                                                    if (columns[17].Substring(0, 1) == "4")
                                                    {
                                                        visatot = visatot + Convert.ToDouble(columns[5]);
                                                    }
                                                    else
                                                    {
                                                        CCtotal = CCtotal + Convert.ToDouble(columns[5]);
                                                    }
                                                    if (columns[16] != "")
                                                    {
                                                        if (columns[17].Substring(0, 1) == "4")
                                                        {
                                                            visafee = visafee + Convert.ToDouble(columns[16]);
                                                        }
                                                        else
                                                        {
                                                            CCFee = CCFee + Convert.ToDouble(columns[16]);
                                                        }

                                                    }
                                                    sw.WriteLine(BannerText);
                                                    sw.WriteLine('\f');
                                                    sw.WriteLine(OpticalHeader);

                                                    OpticalLine = columns[17] + "       " + columns[5];
                                                    while (OpticalLine.Length < 49)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[16];
                                                    while (OpticalLine.Length < 67)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[3];
                                                    sw.WriteLine(OpticalLine);
                                                }
                                            }

                                            //remove period from dollar amounts, if there is no period add 00 to the string to indicate change amount
                                            if (columns[5].Contains("."))
                                            {
                                                columns[5] = columns[5].Replace(".", "");
                                            }
                                            else { columns[5] = columns[5] + "00"; }
                                            if (columns[16].Contains("."))
                                            {
                                                columns[16] = columns[16].Replace(".", "");
                                            }
                                            else { columns[16] = columns[16] + "00"; }
                                            columns[5] = columns[5].Replace("$", "");
                                            Console.WriteLine(columns[5]);
                                            Console.WriteLine(columns[16]);
                                            Console.WriteLine("line 1273 testinst");
                                            if (NewVisaFlag == true)
                                            {
                                                VisaAmount = VisaAmount + (Convert.ToDouble(columns[5]) - Convert.ToDouble(columns[16]));//total amount for trailer lines **subtract the fee amount from the payment line
                                            }
                                            else
                                            {
                                                OldVisaAmount = OldVisaAmount + (Convert.ToDouble(columns[5]) - Convert.ToDouble(columns[16]));//total amount for trailer lines **subtract the fee amount from the payment line
                                            }

                                            string vma = Convert.ToString(Convert.ToDouble(columns[5]) - Convert.ToDouble(columns[16]));
                                            vma = vma.Substring(0, vma.Length - 1);
                                            num = Convert.ToInt16(columns[5].Substring(columns[5].Length - 1, 1));//trail character for amount
                                                                                                                  //lineamount=payment amount - fee amount                                                                                                            //lineamount = Convert.ToString((Convert.ToInt32(columns[5]) - Convert.ToInt32(columns[5]))).Substring(0, Convert.ToString((Convert.ToInt32(columns[5]) - Convert.ToInt32(columns[16]))).Length - 1) + poschar[num];
                                            lineamount = vma + poschar[num];

                                            //Add filler 0's to line amount
                                            while (lineamount.Length < 7) { lineamount = "0" + lineamount; }

                                            long z;
                                            bool isNumeric = long.TryParse(columns[17], out z);
                                            string temp = columns[17];
                                            while (isNumeric == false && temp.Length > 10)
                                            {
                                                temp = temp.Substring(1, temp.Length - 1).TrimStart().TrimEnd();
                                                isNumeric = long.TryParse(temp, out z);
                                            }
                                            Console.WriteLine(temp);
                                            //Each data line will be formatted 5 + card number + date + amount + P
                                            dataline = "5" + /*columns[24]*/temp + columns[3].Substring(0, 2) + columns[3].Substring(3, 2) + columns[3].Substring(columns[3].Length - 2, 2) + lineamount + "P";
                                            line = dataline + "                                                ";
                                            if (NewVisaFlag == true) { VisaCount++; }
                                            else { OldVisaCount++; }
                                            WriteToFile(Filename, line);

                                            line = columns[17] + "|" + columns[3] + "|" + columns[5] + "|" + columns[16] + "|C" /*+ "|" + columns[22] + "|" + columns[18] + "|" + columns[11]*/;
                                            WriteToFile(testfile, line);

                                        }
                                        //File created will need to be placed into Episys so that the transactions can be posted to the member account
                                        else if (((columns[17].Substring(0, 1) == "1" || columns[17].Substring(0, 1) == "2") && (columns[10].Contains("Pending") || columns[10].Contains("Settled")) && columns[17].Length == 13) || (VisaFlag == true && (columns[10].Contains("Pending") || columns[10].Contains("Settled"))))//create file to be sent to episys as letter file
                                        {

                                            if (File.Exists(OpticalPayment))
                                            {
                                                using (StreamWriter sw = File.AppendText(OpticalPayment))
                                                {

                                                    SLTot = SLTot + Convert.ToDouble(columns[5]);

                                                    if (columns[16] != "")
                                                    {
                                                        SLFee = SLFee + Convert.ToDouble(columns[16]);
                                                    }
                                                    OpticalLine = columns[17] + "       " + columns[5];
                                                    while (OpticalLine.Length < 49)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[16];
                                                    while (OpticalLine.Length < 67)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[3];
                                                    sw.WriteLine(OpticalLine);
                                                }
                                            }
                                            else
                                            {
                                                using (StreamWriter sw = new StreamWriter(OpticalPayment, true))
                                                {
                                                    string ReportTitle = "EPX Payment Report";
                                                    string RepCat = "CREDEBCARD";
                                                    string BannerText = BannerPageCreation(RepCat, ReportTitle);
                                                    SLTot = SLTot + Convert.ToDouble(columns[5]);
                                                    if (columns[16] != "")
                                                    {
                                                        SLFee = SLFee + Convert.ToDouble(columns[16]);
                                                    }

                                                    sw.WriteLine(BannerText);
                                                    sw.WriteLine('\f');
                                                    sw.WriteLine(OpticalHeader);

                                                    OpticalLine = columns[17] + "       " + columns[5];
                                                    while (OpticalLine.Length < 49)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[16];
                                                    while (OpticalLine.Length < 67)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[3];
                                                    sw.WriteLine(OpticalLine);
                                                }
                                            }

                                            line = columns[17] + "|" + columns[3] + "|" + columns[5] + "|" + columns[16] + "|C" /*+ "|" + columns[22] + "|" + columns[18] + "|" + columns[11]*/;
                                            WriteToFile(testfile, line);

                                        }

                                        else//create exception file 
                                        {
                                            if (columns[17].TrimEnd().TrimStart() != "")
                                            {
                                                ExceptionFile = SourceDirectory + "exception.txt";
                                                WriteToFile(ExceptionFile, i);

                                            }

                                        }
                                    }
                                    else
                                    {
                                        ExceptionFile = SourceDirectory + "exception.txt";
                                        WriteToFile(ExceptionFile, i);

                                    }
                                }//largeCCFile
                                else //short file
                                {

                                    double tempnum = 0;
                                    bool result = double.TryParse(columns[14], out tempnum);
                                    if (columns[14] != "" && result == true)
                                    {
                                        columns[5] = columns[5].Replace("$", "");
                                        columns[13] = columns[13].Replace("$", "");

                                        //CheckDec
                                        if (columns[13].Contains(basefee))
                                        {
                                            columns[13] = feeamt;
                                        }
                                        else
                                        {
                                            columns[13] = "0.00";
                                        }//end Dec
                                        VisaFlag = false;
                                        if ((columns[14].Substring(0, 1) == "4" || columns[14].Substring(0, 1) == "5") && columns[14].Length == 16)
                                        {
                                            VisaFlag = true;
                                            if (columns[14].Substring(0, 1) == "5")
                                            {
                                                NewVisaFlag = true;
                                                Filename = VisaFilename;
                                            }
                                            else
                                            {
                                                NewVisaFlag = false;
                                                Filename = OldFileName;
                                            }
                                        }
                                        if (VisaFlag == true && (columns[4].Contains("Pending") || columns[4].Contains("Settled") || columns[4].Contains("Purchase")) && columns[8] != "")
                                        {

                                            if (File.Exists(OpticalFile))
                                            {
                                                using (StreamWriter sw = File.AppendText(OpticalFile))
                                                {

                                                    if (columns[14].Substring(0, 1) == "4")
                                                    {
                                                        visatot = visatot + Convert.ToDouble(columns[5]);
                                                    }
                                                    else
                                                    {
                                                        CCtotal = CCtotal + Convert.ToDouble(columns[5]);
                                                    }
                                                    if (columns[13] != "")
                                                    {
                                                        if (columns[14].Substring(0, 1) == "4")
                                                        {
                                                            visafee = visafee + Convert.ToDouble(columns[13]);
                                                        }
                                                        else
                                                        {
                                                            CCFee = CCFee + Convert.ToDouble(columns[13]);
                                                        }

                                                    }
                                                    //visatot = visatot + Convert.ToDouble(columns[5].Substring(1, columns[5].Length - 1));
                                       /*             if (columns[13] != "")
                                                    {
                                                        visafee = visafee + Convert.ToDouble(columns[13]);
                                                        //visafee = visafee + Convert.ToDouble(columns[13].Substring(1, columns[13].Length - 1));
                                                    }
                                                    */
                                                    OpticalLine = columns[14] + "       " + columns[5];
                                                    while (OpticalLine.Length < 49)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[13];
                                                    while (OpticalLine.Length < 67)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[1];
                                                    sw.WriteLine(OpticalLine);
                                                }
                                            }
                                            else
                                            {
                                                using (StreamWriter sw = new StreamWriter(OpticalFile, true))
                                                {
                                                    string ReportTitle = "EPX Credit Card Report";
                                                    string RepCat = "CREDEBCARD";
                                                    string BannerText = BannerPageCreation(RepCat, ReportTitle);



                                                    //visatot = visatot + Convert.ToDouble(columns[5].Substring(1,columns[5].Length-1));
                                                    if (columns[14].Substring(0, 1) == "4")
                                                    {
                                                        visatot = visatot + Convert.ToDouble(columns[5]);
                                                    }
                                                    else
                                                    {
                                                        CCtotal = CCtotal + Convert.ToDouble(columns[5]);
                                                    }
                                                    if (columns[13] != "")
                                                    {
                                                        if (columns[14].Substring(0, 1) == "4")
                                                        {
                                                            visafee = visafee + Convert.ToDouble(columns[13]);
                                                        }
                                                        else
                                                        {
                                                            CCFee = CCFee + Convert.ToDouble(columns[13]);
                                                        }

                                                    }

                                                    sw.WriteLine(BannerText);
                                                    sw.WriteLine('\f');
                                                    sw.WriteLine(OpticalHeader);

                                                    OpticalLine = columns[14] + "       " + columns[5];
                                                    while (OpticalLine.Length < 49)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[13];
                                                    while (OpticalLine.Length < 67)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[1];
                                                    sw.WriteLine(OpticalLine);
                                                }
                                            }

                                            //remove period from dollar amounts, if there is no period add 00 to the string to indicate change amount
                                            if (columns[5].Contains("."))
                                            {
                                                columns[5] = columns[5].Replace(".", "");
                                            }
                                            else { columns[5] = columns[5] + "00"; }
                                            if (columns[13].Contains("."))
                                            {
                                                columns[13] = columns[13].Replace(".", "");
                                            }
                                            else { columns[13] = columns[13] + "00"; }
                                            columns[5] = columns[5].Replace("$", "");
                                            Console.WriteLine(columns[5]);
                                            Console.WriteLine(columns[13]);
                                            Console.WriteLine("line 1529 testinst");
                                            if (NewVisaFlag == true)
                                            {
                                                VisaAmount = VisaAmount + (Convert.ToDouble(columns[5]) - Convert.ToDouble(columns[13]));//total amount for trailer lines **subtract the fee amount from the payment line
                                            }
                                            else
                                            {
                                                OldVisaAmount = OldVisaAmount + (Convert.ToDouble(columns[5]) - Convert.ToDouble(columns[13]));//total amount for trailer lines **subtract the fee amount from the payment line
                                            }
                                            string vma = Convert.ToString(Convert.ToDouble(columns[5]) - Convert.ToDouble(columns[13]));
                                            vma = vma.Substring(0, vma.Length - 1);
                                            num = Convert.ToInt16(columns[5].Substring(columns[5].Length - 1, 1));//trail character for amount
                                                                                                                  //lineamount=payment amount - fee amount                                                                                                             //lineamount = Convert.ToString((Convert.ToInt32(columns[5]) - Convert.ToInt32(columns[5]))).Substring(0, Convert.ToString((Convert.ToInt32(columns[5]) - Convert.ToInt32(columns[16]))).Length - 1) + poschar[num];
                                            lineamount = vma + poschar[num];

                                            //Add filler 0's to line amount
                                            while (lineamount.Length < 7) { lineamount = "0" + lineamount; }

                                            long z;
                                            bool isNumeric = long.TryParse(columns[14], out z);
                                            string temp = columns[14];
                                            while (isNumeric == false && temp.Length > 10)
                                            {
                                                temp = temp.Substring(1, temp.Length - 1).TrimStart().TrimEnd();
                                                isNumeric = long.TryParse(temp, out z);
                                            }
                                            Console.WriteLine(temp);
                                            //Each data line will be formatted 5 + card number + date + amount + P
                                            dataline = "5" + temp + columns[1].Substring(0, 2) + columns[1].Substring(3, 2) + columns[1].Substring(columns[1].Length - 2, 2) + lineamount + "P";
                                            line = dataline + "                                                ";
                                            if (NewVisaFlag == true) { VisaCount++; }
                                            else { OldVisaCount++; }
                                            WriteToFile(Filename, line);

                                            //add to episys file
                                            line = columns[14] + "|" + columns[1] + "|" + columns[5] + "|" + columns[13] + "|C" /*+ "|" + columns[22] + "|" + columns[18] + "|" + columns[11]*/;
                                            WriteToFile(testfile, line);

                                        }
                                        else if (columns[7] != "Void" && ((columns[14].Substring(0, 1) == "1" || columns[14].Substring(0, 1) == "2") && (columns[4].Contains("Pending") || columns[4].Contains("Settled") || columns[4].Contains("Purchase")) && columns[14].Length == 13 && columns[8] != "") || (VisaFlag == true && (columns[4].Contains("Pending") || columns[4].Contains("Settled") || columns[4].Contains("Purchase")) && columns[8] != ""))//create file to be sent to episys as letter file
                                        {
                                            if (File.Exists(OpticalPayment))
                                            {
                                                using (StreamWriter sw = File.AppendText(OpticalPayment))
                                                {

                                                    SLTot = SLTot + Convert.ToDouble(columns[5]);
                                                    if (columns[13] != "")
                                                    {
                                                        SLFee = SLFee + Convert.ToDouble(columns[13]);
                                                    }

                                                    OpticalLine = columns[14] + "       " + columns[5];
                                                    while (OpticalLine.Length < 49)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[13];
                                                    while (OpticalLine.Length < 67)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[1];
                                                    sw.WriteLine(OpticalLine);
                                                }
                                            }
                                            else
                                            {
                                                using (StreamWriter sw = new StreamWriter(OpticalPayment, true))
                                                {
                                                    string ReportTitle = "EPX Payment Report";
                                                    string RepCat = "CREDEBCARD";
                                                    string BannerText = BannerPageCreation(RepCat, ReportTitle);

                                                    Console.WriteLine(columns[5]);


                                                    SLTot = SLTot + Convert.ToDouble(columns[5].Substring(1, columns[5].Length - 1));

                                                    if (columns[13] != "")
                                                    {
                                                        SLFee = SLFee + Convert.ToDouble(columns[13].Substring(1, columns[13].Length - 1));
                                                    }

                                                    sw.WriteLine(BannerText);
                                                    sw.WriteLine('\f');
                                                    sw.WriteLine(OpticalHeader);

                                                    OpticalLine = columns[14] + "       " + columns[5];
                                                    while (OpticalLine.Length < 49)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[13];
                                                    while (OpticalLine.Length < 67)
                                                    {
                                                        OpticalLine = OpticalLine + " ";
                                                    }
                                                    OpticalLine = OpticalLine + columns[1];
                                                    sw.WriteLine(OpticalLine);
                                                }
                                            }

                                            line = columns[14] + "|" + columns[1] + "|" + columns[5] + "|" + columns[13] + "|C" /*+ "|" + columns[22] + "|" + columns[18] + "|" + columns[11]*/;
                                            WriteToFile(testfile, line);
                                        }
                                        else//create exception file 
                                        {
                                            if (columns[14].TrimEnd().TrimStart() != "")
                                            {
                                                ExceptionFile = SourceDirectory + "exception.txt";
                                                WriteToFile(ExceptionFile, i);
                                            }

                                        }
                                    }
                                    else
                                    {
                                        ExceptionFile = SourceDirectory + "exception.txt";
                                        WriteToFile(ExceptionFile, i);

                                    }

                                }//end short file
                            }
                        }//CCRECON


                        //move file to archive folder after it has finished being processed
                        String MoveFile = System.IO.Path.GetFileName(f);
                        Destination = System.IO.Path.Combine(ArchiveDirectory, MoveFile);
                        Destination = Destination.Substring(0, Destination.Length - 4) + datestamp + ".txt";
                        System.IO.File.Move(f, Destination);
                        Console.WriteLine("___ {0} was moved to {1}", System.IO.Path.GetFileNameWithoutExtension(f), ArchiveDirectory);

                        //File.Delete(f);
                    }
                }

                if (File.Exists(OpticalFile))//visa file
                {
                    using (StreamWriter sw = File.AppendText(OpticalFile))
                    {
                        sw.WriteLine("\n");
                        sw.WriteLine("Total Visa Payment: " + visatot.ToString("0.00"));
                        sw.WriteLine("Total Visa Fee: " + visafee.ToString("0.00"));
                        sw.WriteLine("Total Visa Pay-Fee: " + (visatot - visafee).ToString("0.00"));
                        sw.WriteLine("\n");
                        sw.WriteLine("Total Non-Visa Payment: " + CCtotal.ToString("0.00"));
                        sw.WriteLine("Total Non-Visa Fee: " + CCFee.ToString("0.00"));
                        sw.WriteLine("Total Non-Visa Pay-Fee: " + (CCtotal - CCFee).ToString("0.00"));


                    }
                }
                if (File.Exists(OpticalPayment))//visa file
                {
                    using (StreamWriter sw = File.AppendText(OpticalPayment))
                    {
                        sw.WriteLine("\n");
                        sw.WriteLine("Total Payment: " + SLTot.ToString());
                        sw.WriteLine("Total Fee: " + SLFee.ToString());
                        sw.WriteLine("Total Pay-Fee: " + (SLTot - SLFee).ToString());
                    }
                }

                WriteTap(VisaFilename, NewLine8, NewLine9, VisaAmount, VisaCount, poschar);
                WriteTap(OldFileName, Line8, Line9, OldVisaAmount, OldVisaCount, poschar);
                MergeFile(OldFileName, VisaFilename, Fname);
            }

            catch (Exception e)//catches and prints all errors
            {
                Console.WriteLine("The process failed: {0}", e.ToString());
            }
        }
        private static void WriteTap(string FName, string L8, string L9, double Amt, int C, string[] poschar)
        {
            //format the count variable for the trailer lines
            string VCount = Convert.ToString(C);
            int num = Convert.ToInt16(VCount.Substring(VCount.Length - 1, 1));
            VCount = VCount.Substring(0, VCount.Length - 1) + poschar[num];
            while (VCount.Length < 7)
            {
                VCount = "0" + VCount;
            }
            //format the amount variable for the trailer lines
            string VAmount = Convert.ToString(Amt);
            num = Convert.ToInt16(VAmount.Substring(VAmount.Length - 1, 1));
            VAmount = VAmount.Substring(0, VAmount.Length - 1) + poschar[num];
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
                using (StreamWriter sw = new StreamWriter(FName, true))//if it is create new file
                {
                    sw.WriteLine(Line);
                }
            }
            else//append file after it is created
            {
                using (StreamWriter sw = File.AppendText(FName))
                {
                    sw.WriteLine(Line);
                }
            }
            return;
        }
    }
}
