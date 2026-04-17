using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Printuesi.Server.Enums;

namespace Printuesi.Server.Entities
{
    public class PrintJobObjects
    {
        [Key]
        public string PrintJobObject_ID { get; set; }

        [Required]
        public int Quantity_Requested { get; set; }

        [Required]
        public int Quantity_Printed { get; set; }

        [Required]
        public required string Expiry_Date { get; set; }

        [Required]
        public string? Lot_Num { get; set; }

        [Required]
        public Status Status { get; set; }

        // Foreign key to PrintJobs
        [Required]
        public string PrintJobs_ID { get; set; }  // fixed to string

        [ForeignKey("PrintJob_ID")]
        public PrintJobs PrintJobs { get; set; }

        // Foreign key to LabelTemplate
        [Required]
        public string Label_ID { get; set; }  // fixed to string

        [ForeignKey("Label_ID")]
        public LabelTemplate LabelTemplate { get; set; }

    }
}
