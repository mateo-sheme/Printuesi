using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Printuesi.Server.Helpers;

namespace Printuesi.Server.Entities
{
    public class SupplyUsageLogs
    {
        [Key]
        public string SupplyUsageLogID { get; set; } = ID_Generator.SupplyUsage();

        [Required]
        public float UsedQuantity { get; set; }

        [Required]
        public DateTime UsedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public string SupplyID { get; set; } = string.Empty;

        [ForeignKey("SupplyID")]
        public Supplies Supply { get; set; } = null!;

        // Foreign key to PrintJobs
        [Required]
        public string PrintJobID { get; set; } = string.Empty;

        [ForeignKey("PrintJobID")]
        public PrintJobs PrintJob { get; set; } = null!;
    }
}
