using System;
using System.Collections.Generic;
using System.Text;

namespace EPX_File_Script
{
    class Transaction
    {
        public string Name { get; set; }
        public string Account { get; set; }
        public string FeeAmount { get; set; }
        public string PaymentAmount { get; set; }
        public string PaymentType { get; set; }
        public string PostDate { get; set; }
        public string FileType { get; set; }
        public string Status { get; set; }
        public bool VisaFlag { get; set; }
        public bool NewVisaFlag { get; set; }
    }
}
