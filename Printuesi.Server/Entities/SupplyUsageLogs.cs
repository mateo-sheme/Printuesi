using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Printuesi.Server.Entities
{
    public class SupplyUsageLogs
    {
        [Key]
        public string SupplyUsageLog_ID { get; set; }

        [Required]
        public float Used_Quantity { get; set; }

        [Required]
        public DateTime Used_At { get; set; }

        [Required]
        public string Supply_ID { get; set; }  // fixed to string

        [ForeignKey("Supply_ID")]
        public Supplies Supply { get; set; }

        // Foreign key to PrintJobs
        [Required]
        public string PrintJob_ID { get; set; }

        [ForeignKey("PrintJob_ID")]
        public PrintJobs PrintJob { get; set; }
    }
}
