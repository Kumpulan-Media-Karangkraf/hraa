using System.ComponentModel.DataAnnotations;

namespace HRAnalysis.Models
{
    public class tbl_Profile
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string UpdatedBy { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}
