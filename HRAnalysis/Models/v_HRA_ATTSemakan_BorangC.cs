using System.ComponentModel.DataAnnotations;

namespace HRAnalysis.Models
{
    public class v_HRA_ATTSemakan_BorangC
    {
        [Key]
        public int id { get; set; }
        public DateTime TrkAduan { get; set; }

        public string? nopekerja { get; set; }   
        public string? Catatan { get; set; }
    }
}
