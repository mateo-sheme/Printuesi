using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Printuesi.Server.Enums;
using Printuesi.Server.Helpers;

namespace Printuesi.Server.Entities
{
    public class PrintJobs
    {
        [Key]
        public string PrintJobID { get; set; } = ID_Generator.PrintJob();

        [Required]
        public Status Status { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        public string Notes { get; set; } = string.Empty;

        [Required]
        public string CreatedBy { get; set; } = string.Empty;

        [ForeignKey("CreatedBy")]
        public Users User { get; set; } = null!;

        // Foreign key to Clients
        [Required]
        public string ClientID { get; set; } = string.Empty;

        [ForeignKey("ClientID")]
        public Clients Client { get; set; } = null!;

        // Navigation — one job has many items
        public ICollection<PrintJobObjects> PrintJobObjects { get; set; } = new List<PrintJobObjects>();
    }
}
