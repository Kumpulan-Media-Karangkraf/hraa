using System.ComponentModel.DataAnnotations.Schema;

namespace HRAnalysis.Models
{
    public class v_HRA_ATTSemakan_CutiUmum
    {
        [Column("Trk")] // <-- Change this to match actual column name
        public DateTime Tarikh { get; set; }
        public string? keycode { get; set; }
    }
}
