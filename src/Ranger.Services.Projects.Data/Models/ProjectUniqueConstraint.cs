using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Ranger.Common;

namespace Ranger.Services.Projects.Data
{
    public class ProjectUniqueConstraint : IRowLevelSecurityDbSet
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public Guid ProjectId { get; set; }
        [Required]
        public Guid ApiKey { get; set; }
        [Required]
        [StringLength(140)]
        public string Name { get; set; }
        [Required]
        public string DatabaseUsername { get; set; }
    }
}