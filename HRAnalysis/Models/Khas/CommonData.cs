namespace HRAnalysis.Models.Khas
{
    public class CommonData
    {
        public List<tbl_ATTKesalahan> KesalahanRecords { get; set; }
        public List<v_HRA_AttKesalahan> AttKesalahanRecords { get; set; }
        public List<v_HRA_ATTSemakan_CutiUmum> CutiUmumRecords { get; set; }
        public List<v_HRA_ATTSemakan_BorangA> BorangARecords { get; set; }
        public List<v_HRA_ATTSemakan_BorangC> BorangCRecords { get; set; }
        public List<v_HRA_ATTSemakan_BorangTugasan> TugasanRecords { get; set; }
        public List<v_HRA_ATTSemakan_CutiSkokraf> CutiSkokrafRecords { get; set; }
    }

}
