using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PixelSolution.Models
{
    public class UserDepartment
    {
        [Key]
        public int UserDepartmentId { get; set; }

        public int UserId { get; set; }
        public int DepartmentId { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [ForeignKey("DepartmentId")]
        public virtual Department Department { get; set; } = null!;
    }
}
