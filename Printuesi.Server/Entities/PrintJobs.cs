using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Printuesi.Server.Enums;

namespace Printuesi.Server.Entities
{
    public class PrintJobs
    {
        [Key]
        public string PrintJob_ID { get; set; }

        [Required]
        public Status Status { get; set; }

        [Required]
        public DateTime Created_At { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime? Completed_At { get; set; }

        public string Notes { get; set; } = string.Empty;

        [Required]
        public string Created_By { get; set; }

        [ForeignKey("CreatedBy")]
        public Users User { get; set; }

        // Foreign key to Clients
        [Required]
        public string Client_ID { get; set; }

        [ForeignKey("Client_ID")]
        public Clients Client { get; set; }

        // Navigation — one job has many items
        public ICollection<PrintJobObjects> PrintJobObjects { get; set; }
    }
}
