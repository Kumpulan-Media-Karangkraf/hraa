using HRAnalysis.Data;
using Microsoft.AspNetCore.Mvc;

namespace HRAnalysis.Controllers
{
    public class SemakanPageController : Controller
    {
        private readonly AppDbContext _context;

        public SemakanPageController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Semakan(
     string searchIdStaff,
     string searchNama,
     DateTime? dateFrom,
     DateTime? dateTo,
     int? SelectedRecordId,
     List<string> SelectedUserIds)
        {
            var semakanList = _context.tbl_ATTSemakanV1.AsQueryable();


            if (SelectedRecordId.HasValue)
            {
                semakanList = semakanList.Where(x => x.IdProfile == SelectedRecordId.Value);
            }

            if (!string.IsNullOrEmpty(searchIdStaff))
                semakanList = semakanList.Where(x => x.IdStaff == searchIdStaff);

            if (!string.IsNullOrEmpty(searchNama))
                semakanList = semakanList.Where(x => x.Name.Contains(searchNama));

            if (dateFrom.HasValue)
                semakanList = semakanList.Where(x => x.Trk >= dateFrom.Value);

            if (dateTo.HasValue)
                semakanList = semakanList.Where(x => x.Trk <= dateTo.Value);

            if (SelectedUserIds != null && SelectedUserIds.Any())
                semakanList = semakanList.Where(x => SelectedUserIds.Contains(x.IdStaff));

            return View(semakanList.ToList());
        }

    }
}
