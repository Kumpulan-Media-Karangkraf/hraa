using System.ComponentModel.DataAnnotations;

namespace HRAnalysis.Models
{
    public class v_HRA_ATTSemakan_BorangA
    {
        [Key]
        public int IdAplikasi { get; set; }
        public DateTime Tarikh { get; set; }

        public string? NoPekerja { get; set; }     // Nullable
        public string? Nama { get; set; }          // Nullable
        public string? Catatan { get; set; }       // Nullable
    }

}
