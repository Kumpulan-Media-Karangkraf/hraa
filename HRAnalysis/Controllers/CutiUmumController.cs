using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRAnalysis.Models;
using HRAnalysis.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace HRAnalysis.Controllers
{
    public class CutiUmumController : Controller
    {
        private readonly AppDbContext _context;

        public CutiUmumController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CheckStaffMatch(string idStaff)
        {
            if (string.IsNullOrWhiteSpace(idStaff))
            {
                TempData["Message"] = "Please provide a Staff ID.";
                return RedirectToAction("Index");
            }

            // Retrieve kesalahan records for the staff
            var kesalahanList = _context.tbl_ATTKesalahan
                .Where(k => k.IdStaff.Trim().ToLower() == idStaff.Trim().ToLower())
                .ToList();

            // Retrieve tugasan records for the staff
            var tugasanList = _context.v_HRA_ATTSemakan_BorangTugasan
                .Where(t => t.NoPekerja.Trim().ToLower() == idStaff.Trim().ToLower())
                .ToList();

            bool isFound = false;

            foreach (var kesalahan in kesalahanList)
            {
                foreach (var tugasan in tugasanList)
                {
                    if (kesalahan.Trk.Date >= tugasan.TarikhMula.Date && kesalahan.Trk.Date <= tugasan.TarikhTamat.Date)
                    {
                        isFound = true;

                        // Check if already exists in tbl_ATTSemakanV1
                        var existingRecord = _context.tbl_ATTSemakanV1
                            .FirstOrDefault(s => s.IdStaff.Trim().ToLower() == idStaff.Trim().ToLower() && s.Trk.Date == tugasan.TarikhMula.Date);

                        if (existingRecord != null)
                        {
                            // Update existing record
                            existingRecord.Name = kesalahan.Name;
                            existingRecord.AplikasiName = "Borang Tugasan";
                            existingRecord.IdAplikasi = tugasan.IDTugasan;
                            existingRecord.Catatan = tugasan.Catatan;
                            existingRecord.TarikhMula = tugasan.TarikhMula;
                            existingRecord.TarikhTamat = tugasan.TarikhTamat;
                            existingRecord.BilHari = (float)(tugasan.TarikhTamat - tugasan.TarikhMula).TotalDays + 1;
                            _context.tbl_ATTSemakanV1.Update(existingRecord);
                        }
                        else
                        {
                            // Add new record
                            var newRecord = new tbl_ATTSemakanV1
                            {
                                IdStaff = idStaff,
                                Name = kesalahan.Name,
                                Trk = tugasan.TarikhMula,
                                AplikasiName = "Borang Tugasan",
                                IdAplikasi = tugasan.IDTugasan,
                                Catatan = tugasan.Catatan,
                                TarikhMula = tugasan.TarikhMula,
                                TarikhTamat = tugasan.TarikhTamat,
                                BilHari = (float)(tugasan.TarikhTamat - tugasan.TarikhMula).TotalDays + 1
                            };
                            await _context.tbl_ATTSemakanV1.AddAsync(newRecord);
                        }

                        // Save after each match (optional) or save once after all loops
                        await _context.SaveChangesAsync();
                    }
                }
            }

            if (isFound)
            {
                TempData["Message"] = "Staff Match Found and Processed!";
            }
            else
            {
                TempData["Message"] = "No Match Found.";
            }

            return RedirectToAction("Index");
        }




        [HttpPost]
        public async Task<IActionResult> ProcessAllSemakanMatches(string activeTab = "kesalahan")
        {
            int insertedCutiCount = 0;
            int insertedBorangACount = 0;
            int updatedCount = 0;

            using (IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // === 1. PROCESS CUTI UMUM ===
                    var cutiMatches = from att in _context.v_HRA_AttKesalahan
                                      join cuti in _context.v_HRA_ATTSemakan_CutiUmum
                                      on new { Tarikh = att.Tarikh.Date, att.idstaff }
                                      equals new { Tarikh = cuti.Tarikh.Date, idstaff = cuti.keycode }
                                      select new
                                      {
                                          att.idstaff,
                                          att.Nama,
                                          Trk = att.Tarikh,
                                          Catatan = "Cuti Umum"
                                      };

                    foreach (var record in await cutiMatches.ToListAsync())
                    {
                        bool exists = await _context.tbl_ATTSemakan.AnyAsync(x =>
                            x.IdStaff == record.idstaff &&
                            x.Trk.Date == record.Trk.Date &&
                            x.Catatan == "Cuti Umum");

                        if (!exists)
                        {
                            var newSemakan = new tbl_ATTSemakan
                            {
                                IdStaff = record.idstaff,
                                Name = record.Nama,
                                Trk = record.Trk,
                                Catatan = record.Catatan
                            };

                            _context.tbl_ATTSemakan.Add(newSemakan);
                            await _context.SaveChangesAsync();

                            var existingKesalahan = await _context.tbl_ATTKesalahan
                                .FirstOrDefaultAsync(k =>
                                    k.IdStaff == record.idstaff &&
                                    k.Trk.Date == record.Trk.Date);

                            if (existingKesalahan != null)
                            {
                                existingKesalahan.IdSemakan = newSemakan.IdSemakan;
                                _context.tbl_ATTKesalahan.Update(existingKesalahan);
                                await _context.SaveChangesAsync();
                                updatedCount++;
                            }

                            insertedCutiCount++;
                        }
                    }

                    // === 2. PROCESS BORANG A ===
                    var borangAMatches = from kesalahan in _context.tbl_ATTKesalahan
                                         join borangA in _context.v_HRA_ATTSemakan_BorangA
                                         on new { IdStaff = kesalahan.IdStaff, Tarikh = kesalahan.Trk.Date }
                                         equals new { IdStaff = borangA.NoPekerja, Tarikh = borangA.Tarikh.Date }
                                         select new
                                         {
                                             KesalahanId = kesalahan.Id,
                                             kesalahan.IdStaff,
                                             kesalahan.Name,
                                             kesalahan.Trk,
                                             kesalahan.IdSemakan,
                                             AplikasiName = borangA.Nama,
                                             IdAplikasi = borangA.IdAplikasi,
                                             borangA.Catatan
                                         };

                    foreach (var match in await borangAMatches.ToListAsync())
                    {
                        bool exists = await _context.tbl_ATTSemakan.AnyAsync(x =>
                            x.IdStaff == match.IdStaff &&
                            x.Trk.Date == match.Trk.Date &&
                            x.IdAplikasi == match.IdAplikasi);

                        if (!exists)
                        {
                            var newSemakan = new tbl_ATTSemakan
                            {
                                IdStaff = match.IdStaff,
                                Name = match.Name,
                                Trk = match.Trk,
                                AplikasiName = match.AplikasiName,
                                IdAplikasi = match.IdAplikasi,
                                Catatan = match.Catatan
                            };

                            _context.tbl_ATTSemakan.Add(newSemakan);
                            await _context.SaveChangesAsync();

                            if (match.IdSemakan == null)
                            {
                                var kesalahanToUpdate = await _context.tbl_ATTKesalahan
                                    .FirstOrDefaultAsync(k => k.Id == match.KesalahanId);

                                if (kesalahanToUpdate != null)
                                {
                                    kesalahanToUpdate.IdSemakan = newSemakan.IdSemakan;
                                    _context.tbl_ATTKesalahan.Update(kesalahanToUpdate);
                                    await _context.SaveChangesAsync();
                                    updatedCount++;
                                }
                            }

                            insertedBorangACount++;
                        }
                    }

                    await transaction.CommitAsync();
                    TempData["Message"] = $"Cuti Umum: {insertedCutiCount} inserted. Borang A: {insertedBorangACount} inserted. {updatedCount} records updated.";
                    TempData["ActiveTab"] = activeTab; // Store active tab
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    TempData["Error"] = $"Error occurred during combined processing: {ex.Message}";
                    TempData["ActiveTab"] = activeTab; // Store active tab even on error
                }
            }
            return RedirectToAction("Search", "AttKesalahan", new { activeTab = activeTab });

        }
    }
}