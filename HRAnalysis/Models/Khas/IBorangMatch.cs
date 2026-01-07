namespace HRAnalysis.Models.Khas
{
    public interface IBorangMatch
    {
        string IdStaff { get; set; }
        string Name { get; set; }
        DateTime Trk { get; set; }
        int? IdAplikasi { get; set; }
        string Catatan { get; set; }
    }
}
