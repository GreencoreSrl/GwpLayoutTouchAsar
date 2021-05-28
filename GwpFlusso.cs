using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GwpLayoutTouchAsar
{

    public class GwpFlusso
    {
        public int Store { get; set; }
        public int Terminal { get; set; }
        public DateTime Date { get; set; }
        public string MaintenanceId { get; set; }
        public string MaintenanceType { get; set; }
        public int ReceivedRecords { get; set; }
        public int AppliedRecords { get; set; }
        public int ErrorRecords { get; set; }
        public string Step { get; set; }
        public object Response { get; set; }
    }

}
