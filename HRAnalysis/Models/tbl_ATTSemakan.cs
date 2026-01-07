using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRAnalysis.Models
{
    public class tbl_ATTSemakan
    {
        [Key]
        public int IdSemakan { get; set; }

        [Required]
        public string IdStaff { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public DateTime Trk { get; set; }

        public string? AplikasiName { get; set; }

        public int? IdAplikasi { get; set; }

        public string? Catatan { get; set; }
      

        //[ForeignKey("OriginalKesalahanId")]
        //public virtual tbl_ATTKesalahan? OriginalKesalahan { get; set; }
        //public int? OriginalKesalahanId { get; set; }
    }
}