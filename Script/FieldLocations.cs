using System;
using System.Collections.Generic;
using System.Text;

namespace EPX_File_Script
{
    class FieldLocations
    {
        public int accountLoc { get; set; } 
        public int dateLoc { get; set; }
        public int paymentAmountLoc { get; set; }
        public int feeAmountLoc { get; set; }
        public int statusLoc { get; set; }
        public int paymentTypeLoc { get; set; }
        public bool fieldsMapped { get; set; }

        public FieldLocations() 
        {
            accountLoc = -1;
            dateLoc = -1;
            paymentAmountLoc = -1;
            feeAmountLoc = -1;
            statusLoc = -1;
            paymentTypeLoc = -1;
            fieldsMapped = false;
        }
    }
}
