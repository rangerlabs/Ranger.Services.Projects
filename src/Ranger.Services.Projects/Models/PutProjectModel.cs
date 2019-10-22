using System.ComponentModel.DataAnnotations;

namespace Ranger.Services.Projects
{
    public class PutProjectModel
    {
        [Required]
        public int Version { get; set; }
        [Required]
        [StringLength(140)]
        public string Name { get; set; }
        [Required]
        public bool Enabled { get; set; }
        [Required]
        public string UserEmail { get; set; }
        public string Description { get; set; }
        [Required]
        public string ApiKey { get; set; }
    }
}