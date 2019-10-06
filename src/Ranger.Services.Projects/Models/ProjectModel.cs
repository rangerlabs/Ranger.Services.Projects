using System.ComponentModel.DataAnnotations;

namespace Ranger.Services.Projects
{
    public class ProjectModel
    {
        [Required]
        public string UserEmail { get; set; }
        [Required]
        public string Name { get; set; }
        public string Description { get; set; }
    }
}