using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Printuesi.Server.Enums;

namespace Printuesi.Server.Entities
{
    public class PrintJobObjects
    {
        [Key]
        public Guid PrintJobObjectID { get; set; } = Guid.NewGuid();

        [Required]
        public int Quantity_Requested { get; set; }

        [Required]
        public int Quantity_Printed { get; set; }

        [Required]
        public string Expiry_Date { get; set; } = string.Empty;

        public string? Lot_Num { get; set; }

        [Required]
        public Status Status { get; set; }

        // Foreign key to PrintJobs
        [Required]
        public Guid PrintJobID { get; set; }

        [ForeignKey("PrintJobID")]
        public PrintJobs PrintJob { get; set; } = null!;

        // Foreign key to LabelTemplate
        [Required]
        public Guid LabelID { get; set; }

        [ForeignKey("LabelID")]
        public LabelTemplate LabelTemplate { get; set; } = null!;
    }
}