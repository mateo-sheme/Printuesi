using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Printuesi.Server.Enums;

namespace Printuesi.Server.Entities
{
    public class PrintJobs
    {
        [Key]
        public Guid PrintJobID { get; set; } = Guid.NewGuid();

        [Required]
        public Status Status { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime? CompletedAt { get; set; }

        public string Notes { get; set; } = string.Empty;

        [Required]
        public Guid CreatedBy { get; set; }

        [ForeignKey("CreatedBy")]
        public Users User { get; set; } = null!;

        // Foreign key to Clients
        [Required]
        public Guid ClientID { get; set; }

        [ForeignKey("ClientID")]
        public Clients Client { get; set; } = null!;

        // Navigation — one job has many items
        public ICollection<PrintJobObjects> PrintJobObjects { get; set; } = new List<PrintJobObjects>();
    }
}
