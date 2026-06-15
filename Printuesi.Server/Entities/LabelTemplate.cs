using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Printuesi.Server.Enums;
using Printuesi.Server.Helpers;

namespace Printuesi.Server.Entities
{
    public class LabelTemplate
    {
        [Key]
        public string LabelID { get; set; } = ID_Generator.Template();

        [Required]
        public string ClientID { get; set; } = string.Empty;

        [Required]
        public PaperSize PaperSize { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public string LabelName { get; set; } = string.Empty;

        [Required]
        public string PdfPath { get; set; } = string.Empty;

        [Required]
        public string DateFormat { get; set; } = "dd/MM/yyyy";
        public int ExpiryOffsetMonths { get; set; } = 12;

        [ForeignKey("ClientID")]
        public Clients Clients { get; set; } = null!;
    }
}
