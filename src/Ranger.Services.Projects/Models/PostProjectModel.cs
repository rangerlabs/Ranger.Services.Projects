using System.ComponentModel.DataAnnotations;

namespace Ranger.Services.Projects
{
    public class PostProjectModel
    {
        [Required]
        [StringLength(140)]
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public string Description { get; set; }
    }
}