using System;
using System.Collections.Generic;
using System.Text;

namespace EPX_File_Script
{
    class Totals
    {
        public double visatot { get; set; } = 0.00;
        public double CCtotal { get; set; } = 0.00;
        public double CCFee { get; set; } = 0.00;
        public double visafee { get; set; } = 0.00;
        public double SLTot { get; set; } = 0.00;
        public double SLFee { get; set; } = 0.00;
        public int VisaCount { get; set; } = 0;
        public double VisaAmount { get; set; } = 0.00;
        public int OldVisaCount { get; set; } = 0;
        public double OldVisaAmount { get; set; } = 0.00;
    }
}
