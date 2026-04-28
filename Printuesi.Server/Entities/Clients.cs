using System.ComponentModel.DataAnnotations;
using Printuesi.Server.Enums;

namespace Printuesi.Server.Entities
{
    public class Clients
    {
        [Key]
        public Guid ClientID { get; set; } = Guid.NewGuid();

        [Required]
        public string NIPT { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        //one to many connection to label templates and printjobs so a client can have many labels and many orderds/jobs
        public ICollection<LabelTemplate> LabelTemplate { get; set; } = new List<LabelTemplate>();
        public ICollection<PrintJobs> PrintJobs { get; set; } = new List<PrintJobs>();
    }
}
