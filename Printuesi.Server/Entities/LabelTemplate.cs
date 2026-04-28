using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Printuesi.Server.Enums;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Printuesi.Server.Entities
{
    public class LabelTemplate
    {
        [Key]
        public Guid LabelID { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ClientID { get; set; }

        [Required]
        public PaperSize PaperSize { get; set; }

        [Required]
        public string LabelName { get; set; } = string.Empty;

        [Required]
        public string PdfPath { get; set; } = string.Empty;

        [Required]
        public string DateFormat { get; set; } = string.Empty;          // "dd/MM/yyyy" or "MM/yyyy"
        public int ExpiryOffsetMonths { get; set; }

        [ForeignKey("ClientID")]
        public Clients Clients { get; set; } = null!;
    }
}
