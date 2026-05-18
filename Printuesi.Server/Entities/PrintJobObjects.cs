using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Printuesi.Server.Enums;
using Printuesi.Server.Helpers;

namespace Printuesi.Server.Entities
{
    public class PrintJobObjects
    {
        [Key]
        public string PrintJobObjectID { get; set; } = ID_Generator.PrintJobObject();

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
        public string PrintJobID { get; set; } = string.Empty;

        [ForeignKey("PrintJobID")]
        public PrintJobs PrintJob { get; set; } = null!;

        // Foreign key to LabelTemplate
        [Required]
        public string LabelID { get; set; } = string.Empty;

        [ForeignKey("LabelID")]
        public LabelTemplate LabelTemplate { get; set; } = null!;
    }
}