using System;
using System.Collections.Generic;
using System.Text;

namespace EPX_File_Script
{
    class Totals
    {
        public double visatot { get; set; }
        public double CCtotal { get; set; }
        public double CCFee { get; set; }
        public double visafee { get; set; } 
        public double SLTot { get; set; } 
        public double SLFee { get; set; }
        public int VisaCount { get; set; }
        public double VisaAmount { get; set; }
        public int OldVisaCount { get; set; }
        public double OldVisaAmount { get; set; }

        public Totals() 
        {
            visatot = 0.00;
            CCtotal = 0.00;
            CCFee = 0.00;
            visafee = 0.00;
            SLTot = 0.00;
            SLFee = 0.00;
            VisaCount = 0;
            VisaAmount = 0.00;
            OldVisaCount = 0;
            OldVisaAmount = 0.00;
        }
    }
}
