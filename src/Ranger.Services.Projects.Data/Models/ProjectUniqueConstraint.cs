using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Ranger.Common;

namespace Ranger.Services.Projects.Data
{
    //Enables unique indexes of currently active projects at the tenant level
    public class ProjectUniqueConstraint
    {
        //Foreign Key references are not supported for JsonB columns
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public Guid ProjectId { get; set; }

        [Required]
        [StringLength(41)]
        public string TenantId { get; set; }

        [Required]
        public string HashedLiveApiKey { get; set; }

        [Required]
        public string HashedTestApiKey { get; set; }

        [Required]
        public string HashedProjectApiKey { get; set; }

        [Required]
        [StringLength(140)]
        public string Name { get; set; }
    }
}