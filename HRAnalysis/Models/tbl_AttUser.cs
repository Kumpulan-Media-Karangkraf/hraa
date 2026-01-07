using System.ComponentModel.DataAnnotations;

namespace HRAnalysis.Models
{
    public class tbl_AttUser
    {
      
            [Key]
            [Required(ErrorMessage = "IdUser is required.")]
            public int IdUser { get; set; }

            [Required(ErrorMessage = "FullName is required.")]
            [StringLength(100, ErrorMessage = "FullName cannot be longer than 100 characters.")]
            public string? FullName { get; set; } = "";

            [Required(ErrorMessage = "Username is required.")]
            [StringLength(100, ErrorMessage = "Username cannot be longer than 100 characters.")]
            public string? Username { get; set; } = "";
            public string? Password { get; set; } = "";

            [Required(ErrorMessage = "Active status is required.")]
            public bool Active { get; set; }
            public bool BlockUser { get; set; }

            [StringLength(5, ErrorMessage = "The StaffNum must be a maximum of 5 characters.")]
            public string? IdStaff { get; set; } = "";
            public string? Roles { get; set; } = "";
            public bool UseWindowsAuth { get; set; }
            public DateTime LastUpdate { get; set; }


    }
    }
