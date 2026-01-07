using HRAnalysis.Data;
using HRAnalysis.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRAnalysis.Controllers
{
    public class CutiSkokrafController : Controller
    {

        private readonly AppDbContext _context;

        public CutiSkokrafController(AppDbContext context)
        {
            _context = context;
        }



        [HttpPost]
        public async Task<IActionResult> ScanCutiSkokrafSQL()
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Database.SetCommandTimeout(300); // 5 minutes

                // 1. Insert into tbl_ATTSemakanV1
                var insertSql = @"
            INSERT INTO tbl_ATTSemakanV1 (IdStaff, Name, Trk, AplikasiName, IdAplikasi, Catatan, TarikhMula, TarikhTamat, BilHari)
            SELECT DISTINCT
                k.IdStaff,
                k.Name,
                c.StartDate,
                'Cuti Skokraf',
                CAST(c.NofDays AS INT),
                NULL,
                c.StartDate,
                c.EndDate,
                c.NofDays
            FROM v_HRA_ATTSemakan_CutiSkokraf c
            INNER JOIN (
                SELECT DISTINCT IdStaff, Name, Trk
                FROM tbl_ATTKesalahan
            ) k ON k.IdStaff = c.KeyCode 
                AND CAST(k.Trk AS DATE) = CAST(c.StartDate AS DATE)
            WHERE NOT EXISTS (
                SELECT 1 FROM tbl_ATTSemakanV1 s 
                WHERE s.IdStaff = c.KeyCode 
                AND CAST(s.TarikhMula AS DATE) = CAST(c.StartDate AS DATE)
                AND CAST(s.TarikhTamat AS DATE) = CAST(c.EndDate AS DATE)
                AND s.AplikasiName = 'Cuti Skokraf'
            );";

                int inserted = await _context.Database.ExecuteSqlRawAsync(insertSql);

                // 2. Update tbl_ATTKesalahan with Exclude = 1 and set IdSemakan
                var updateSql = @"
            UPDATE k
            SET 
                k.Exclude = 1,
                k.IdSemakan = s.IdSemakan
            FROM tbl_ATTKesalahan k
            INNER JOIN tbl_ATTSemakanV1 s
                ON k.IdStaff = s.IdStaff
                AND CAST(k.Trk AS DATE) = CAST(s.TarikhMula AS DATE)
                AND s.AplikasiName = 'Cuti Skokraf';";

                await _context.Database.ExecuteSqlRawAsync(updateSql);

                await transaction.CommitAsync();

                TempData["Message"] = inserted > 0
                    ? $"Successfully processed and updated {inserted} records for Cuti Skokraf."
                    : "No matching records found or all records already exist.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Message"] = $"Error occurred while processing: {ex.Message}";
            }
            finally
            {
                _context.Database.SetCommandTimeout(30); // Reset timeout
            }

            return RedirectToAction("ViewKesalahan");
        }


    }
}
