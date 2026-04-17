using System.ComponentModel.DataAnnotations;
using Printuesi.Server.Enums;

namespace Printuesi.Server.Entities
{
    public class Clients
    {
        [Key]
        public string Client_ID { get; set; }

        [Required]
        public string NIPT { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public DateTime Created_At { get; set; } = DateTime.UtcNow;

        //one to many connection to label templates and printjobs so a client can have many labels and many orderds/jobs
        public ICollection<LabelTemplate> LabelTemplate { get; set; }
        public ICollection<PrintJobs> PrintJobs { get; set; }
    }
}
