using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Printuesi.Server.Entities
{
    public class SupplyUsageLogs
    {
        [Key]
        public Guid SupplyUsageLogID { get; set; } = Guid.NewGuid();

        [Required]
        public float UsedQuantity { get; set; }

        [Required]
        public DateTime UsedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public Guid SupplyID { get; set; }

        [ForeignKey("SupplyID")]
        public Supplies Supply { get; set; } = null!;

        // Foreign key to PrintJobs
        [Required]
        public Guid PrintJobID { get; set; }

        [ForeignKey("PrintJobID")]
        public PrintJobs PrintJob { get; set; } = null!;
    }
}
