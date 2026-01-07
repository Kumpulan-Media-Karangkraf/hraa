using System.ComponentModel.DataAnnotations;

namespace HRAnalysis.Models
{
    public class v_HRA_ATTSemakan_CutiSkokraf
    {
        [Key]
        public string KeyCode { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double NofDays { get; set; }
        public string Stgh { get; set; }

        public int id { get; set; }
    }
}
