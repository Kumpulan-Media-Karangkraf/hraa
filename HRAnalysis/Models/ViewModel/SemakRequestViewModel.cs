namespace HRAnalysis.Models.ViewModel
{
    public class SemakRequestViewModel
    {
        public List<int> SelectedKesalahanIds { get; set; } = new List<int>();
        public bool ProcessAll { get; set; } = false;
    }
}
