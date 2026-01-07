using HRAnalysis.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using HRAnalysis.Data;

namespace HRAnalysis.Controllers
{
    public class ProsesSemakanController : Controller
    {
        private readonly AppDbContext _context;

        public ProsesSemakanController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> ProcessAllSemakanMatches(
    string searchIdStaff = "",
     List<string> SelectedUserIds = null,
    string searchNama = "",
    DateTime? dateFrom = null,
    DateTime? dateTo = null,
    string activeTab = "kesalahan",
    int? SelectedRecordId = null)
        {
            int insertedCutiCount = 0;
            int insertedBorangACount = 0;
            int insertedBorangCCount = 0;
            int insertedTugasanCount = 0;
            int updatedCount = 0;
            int insertedCutiSkokrafCount = 0;
            
            var loggedInUsername = HttpContext.Session.GetString("Username");


            try
            {
                if (SelectedRecordId.HasValue)
                {
                    var recordsToDelete = _context.tbl_ATTSemakanV1
                        .Where(x => x.IdProfile == SelectedRecordId.Value);

                    if (recordsToDelete.Any())
                    {
                        _context.tbl_ATTSemakanV1.RemoveRange(recordsToDelete);
                        await _context.SaveChangesAsync();
                    }
                }
                // Optional: handle case where SelectedRecordId is null
                // else
                // {
                //     // Log: "No ProfileId provided for deletion"
                // }
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error deleting previous semakan records: " + ex.Message);
            }


            using (IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // === 1. PROCESS CUTI UMUM (FIXED VERSION) ===

                    //try fetchtbl kesalahan instead of v_hra_attkesalhan

                    var cutiQuery = from att in _context.v_HRA_AttKesalahan
                                    join cuti in _context.v_HRA_ATTSemakan_CutiUmum
                                    on new { Tarikh = att.Tarikh.Date, att.idstaff }
                                    equals new { Tarikh = cuti.Tarikh.Date, idstaff = cuti.keycode }
                                    select new
                                    {
                                        att.idstaff,
                                        att.Nama,
                                        Trk = att.Tarikh,
                                        Catatan = "-"
                                    };

                    // Apply filters
                    if (!string.IsNullOrEmpty(searchIdStaff))
                        cutiQuery = cutiQuery.Where(x => x.idstaff.Contains(searchIdStaff));
                    if (!string.IsNullOrEmpty(searchNama))
                        cutiQuery = cutiQuery.Where(x => x.Nama.Contains(searchNama));
                    if (SelectedUserIds != null && SelectedUserIds.Any())
                    {
                        cutiQuery = cutiQuery.Where(x => SelectedUserIds.Contains(x.idstaff));
                    }
                    if (dateFrom.HasValue)
                        cutiQuery = cutiQuery.Where(x => x.Trk.Date >= dateFrom.Value.Date);
                    if (dateTo.HasValue)
                        cutiQuery = cutiQuery.Where(x => x.Trk.Date <= dateTo.Value.Date);

                    // ✅ ADD DEBUGGING TO SEE WHAT'S BEING MATCHED
                    var cutiResults = await cutiQuery.ToListAsync();
                    Console.WriteLine($"📊 Found {cutiResults.Count} Cuti Umum matches");

                    foreach (var record in cutiResults)
                    {
                        Console.WriteLine($"🔍 Processing Cuti Umum: Staff={record.idstaff}, Date={record.Trk:yyyy-MM-dd}");

                        // ✅ MODIFIED: Check existing semakan with proper date comparison AND UpdatedBy check
                        var existingSemakan = await _context.tbl_ATTSemakanV1
                            .FirstOrDefaultAsync(x => x.IdStaff == record.idstaff &&
                                                      x.Trk.Date == record.Trk.Date &&
                                                      x.AplikasiName == "Cuti Umum" &&
                                                        x.IdProfile == SelectedRecordId &&
                                                      x.UpdatedBy == loggedInUsername); // ✅ Added UpdatedBy check

                        if (existingSemakan == null)
                        {
                            // ✅ Check if there's any existing record with different UpdatedBy
                            var existingDifferentUser = await _context.tbl_ATTSemakanV1
                                .FirstOrDefaultAsync(x => x.IdStaff == record.idstaff &&
                                                          x.Trk.Date == record.Trk.Date &&
                                                          x.AplikasiName == "Cuti Umum" &&
                                                            x.IdProfile == SelectedRecordId &&
                                                          x.UpdatedBy != loggedInUsername);

                            if (existingDifferentUser != null)
                            {
                                Console.WriteLine($"⚠️ Found existing record with different UpdatedBy: {existingDifferentUser.UpdatedBy}. Creating new record for {loggedInUsername}");
                            }

                            var newSemakan = new tbl_ATTSemakanV1
                            {
                                IdStaff = record.idstaff,
                                Name = record.Nama,
                                Trk = record.Trk,
                                AplikasiName = "Cuti Umum",
                                Catatan = "Cuti Umum",
                                UpdatedBy = loggedInUsername,
                                IdProfile = SelectedRecordId
                            };
                            _context.tbl_ATTSemakanV1.Add(newSemakan);
                            await _context.SaveChangesAsync();
                            insertedCutiCount++;

                            Console.WriteLine($"✅ Created new Cuti Umum semakan: {record.idstaff} | {record.Trk:yyyy-MM-dd} | UpdatedBy: {loggedInUsername} | IdSemakan: {newSemakan.IdSemakan}");

                            // ✅ FIXED: Update ALL kesalahan records for this staff and date that don't have IdSemakan yet
                            var kesalahanRecords = await _context.tbl_ATTKesalahan
                                .Where(k => k.IdStaff == record.idstaff &&
                                           k.Trk.Date == record.Trk.Date &&
                                             k.IdProfile == SelectedRecordId &&
                                           k.IdSemakan == null)
                                .ToListAsync();

                            Console.WriteLine($"📝 Found {kesalahanRecords.Count} kesalahan records to update for {record.idstaff} on {record.Trk:yyyy-MM-dd}");

                            foreach (var kesalahan in kesalahanRecords)
                            {
                                kesalahan.IdProfile = SelectedRecordId;
                                kesalahan.IdSemakan = newSemakan.IdSemakan;
                                kesalahan.Exclude = true;

                                var originalKesalahanRecord = await _context.v_HRA_AttKesalahan
                                    .FirstOrDefaultAsync(v => v.idstaff == kesalahan.IdStaff && v.Tarikh.Date == kesalahan.Trk.Date);
                                if (originalKesalahanRecord != null)
                                {
                                    kesalahan.Syrt = originalKesalahanRecord.Syrt;
                                    kesalahan.Jab = originalKesalahanRecord.Jab;
                                }
                                _context.tbl_ATTKesalahan.Update(kesalahan);
                                updatedCount++;

                                Console.WriteLine($"🔄 Updated kesalahan: {kesalahan.IdStaff} | {kesalahan.Trk:yyyy-MM-dd} | {kesalahan.JenisKesalahan} | IdSemakan: {kesalahan.IdSemakan}");
                            }
                            await _context.SaveChangesAsync();
                        }
                        else
                        {
                            Console.WriteLine($"ℹ️ Existing Cuti Umum semakan found for current user: {record.idstaff} | {record.Trk:yyyy-MM-dd} | UpdatedBy: {loggedInUsername} | IdSemakan: {existingSemakan.IdSemakan}");

                            bool needUpdate = false;

                            if (existingSemakan.Name != record.Nama)
                            {
                                existingSemakan.Name = record.Nama;
                                needUpdate = true;
                            }
                            if (existingSemakan.Catatan != record.Catatan)
                            {
                                existingSemakan.Catatan = record.Catatan;
                                needUpdate = true;
                            }

                            if (needUpdate)
                            {
                                _context.tbl_ATTSemakanV1.Update(existingSemakan);
                                await _context.SaveChangesAsync();
                                updatedCount++;
                                Console.WriteLine($"🔄 Updated existing Cuti Umum semakan: {record.idstaff} | {record.Trk:yyyy-MM-dd}");
                            }

                            // ✅ FIXED: Update kesalahan records that don't have this IdSemakan yet
                            var kesalahanRecords = await _context.tbl_ATTKesalahan
                                .Where(k => k.IdStaff == record.idstaff &&
                                           k.Trk.Date == record.Trk.Date &&
                                            k.IdProfile == SelectedRecordId &&
                                           (k.IdSemakan == null || k.IdSemakan != existingSemakan.IdSemakan))
                                .ToListAsync();

                            Console.WriteLine($"📝 Found {kesalahanRecords.Count} kesalahan records to update for existing semakan {record.idstaff} on {record.Trk:yyyy-MM-dd}");

                            foreach (var kesalahan in kesalahanRecords)
                            {
                                kesalahan.IdSemakan = existingSemakan.IdSemakan;
                                kesalahan.Exclude = true;

                                // ✅ UPDATE SYRT AND JAB FROM ORIGINAL RECORD
                                var originalKesalahanRecord = await _context.v_HRA_AttKesalahan
                                    .FirstOrDefaultAsync(v => v.idstaff == kesalahan.IdStaff && v.Tarikh.Date == kesalahan.Trk.Date);
                                if (originalKesalahanRecord != null)
                                {
                                    kesalahan.Syrt = originalKesalahanRecord.Syrt;
                                    kesalahan.Jab = originalKesalahanRecord.Jab;
                                }
                                _context.tbl_ATTKesalahan.Update(kesalahan);
                                updatedCount++;

                                Console.WriteLine($"🔄 Updated kesalahan for existing semakan: {kesalahan.IdStaff} | {kesalahan.Trk:yyyy-MM-dd} | {kesalahan.JenisKesalahan} | IdSemakan: {kesalahan.IdSemakan}");
                            }
                            await _context.SaveChangesAsync();
                        }
                    }


                    // === 2. PROCESS BORANG A ===
                    var borangAQuery = from kesalahan in _context.tbl_ATTKesalahan
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
                                           kesalahan.JenisKesalahan, // Add this to get the JenisKesalahan
                                           AplikasiName = "Borang A",
                                           IdAplikasi = borangA.IdAplikasi,
                                           borangA.Catatan
                                       };

                    if (!string.IsNullOrEmpty(searchIdStaff))
                        borangAQuery = borangAQuery.Where(x => x.IdStaff.Contains(searchIdStaff));
                    if (!string.IsNullOrEmpty(searchNama))
                        borangAQuery = borangAQuery.Where(x => x.Name.Contains(searchNama));

                    if (SelectedUserIds != null && SelectedUserIds.Any())
                    {
                        borangAQuery = borangAQuery.Where(x => SelectedUserIds.Contains(x.IdStaff));
                    }

                    if (dateFrom.HasValue)
                        borangAQuery = borangAQuery.Where(x => x.Trk.Date >= dateFrom.Value.Date);
                    if (dateTo.HasValue)
                        borangAQuery = borangAQuery.Where(x => x.Trk.Date <= dateTo.Value.Date);

                    var borangAResults = await borangAQuery.ToListAsync();
                    Console.WriteLine($"📊 Found {borangAResults.Count} Borang A matches");

                    foreach (var match in borangAResults)
                    {
                        Console.WriteLine($"🔍 Processing Borang A: Staff={match.IdStaff}, Date={match.Trk:yyyy-MM-dd}");

                        // ✅ MODIFIED: Check existing semakan with UpdatedBy check
                        var existingSemakan = await _context.tbl_ATTSemakanV1
                            .FirstOrDefaultAsync(x =>
                                x.IdStaff == match.IdStaff &&
                                x.Trk.Date == match.Trk.Date &&
                                x.IdAplikasi == match.IdAplikasi &&
                                x.AplikasiName == "Borang A" &&
                                 x.IdProfile == SelectedRecordId &&
                                x.UpdatedBy == loggedInUsername); // ✅ Added UpdatedBy check

                        if (existingSemakan == null)
                        {
                            // ✅ Check if there's any existing record with different UpdatedBy
                            var existingDifferentUser = await _context.tbl_ATTSemakanV1
                                .FirstOrDefaultAsync(x =>
                                    x.IdStaff == match.IdStaff &&
                                    x.Trk.Date == match.Trk.Date &&
                                    x.IdAplikasi == match.IdAplikasi &&
                                    x.AplikasiName == "Borang A" &&
                                     x.IdProfile == SelectedRecordId &&
                                    x.UpdatedBy != loggedInUsername);

                            if (existingDifferentUser != null)
                            {
                                Console.WriteLine($"⚠️ Found existing Borang A with different UpdatedBy: {existingDifferentUser.UpdatedBy}. Creating new record for {loggedInUsername}");
                            }

                            var newSemakan = new tbl_ATTSemakanV1
                            {
                                IdStaff = match.IdStaff,
                                Name = match.Name,
                                Trk = match.Trk,
                                AplikasiName = "Borang A",
                                IdAplikasi = match.IdAplikasi,
                                Catatan = match.Catatan,
                                UpdatedBy = loggedInUsername,
                                IdProfile = SelectedRecordId
                            };
                            _context.tbl_ATTSemakanV1.Add(newSemakan);
                            await _context.SaveChangesAsync();
                            insertedBorangACount++;

                            Console.WriteLine($"✅ Created new Borang A semakan: {match.IdStaff} | {match.Trk:yyyy-MM-dd} | UpdatedBy: {loggedInUsername} | IdSemakan: {newSemakan.IdSemakan}");

                            // ✅ UPDATE ALL KESALAHAN RECORDS FOR THIS STAFF AND DATE
                            var kesalahanRecords = await _context.tbl_ATTKesalahan
                                .Where(k => k.IdStaff == match.IdStaff &&
                                           k.Trk.Date == match.Trk.Date &&
                                            k.IdProfile == SelectedRecordId &&
                                           k.IdSemakan == null)
                                .ToListAsync();

                            Console.WriteLine($"📝 Found {kesalahanRecords.Count} kesalahan records to update for {match.IdStaff} on {match.Trk:yyyy-MM-dd}");

                            foreach (var kesalahan in kesalahanRecords)
                            {
                                // ✅ SPECIAL HANDLING FOR "Balik Awal" - NO IdSemakan, Exclude = false
                                if (kesalahan.JenisKesalahan == "Balik Awal")
                                {
                                    kesalahan.IdSemakan = null;
                                    kesalahan.Exclude = null; //false
                                
                                }
                                else
                                {
                                    kesalahan.IdSemakan = newSemakan.IdSemakan;
                                    kesalahan.Exclude = true;
                                }

                                // ✅ UPDATE SYRT AND JAB FROM ORIGINAL RECORD
                                var originalKesalahanRecord = await _context.v_HRA_AttKesalahan
                                    .FirstOrDefaultAsync(v => v.idstaff == kesalahan.IdStaff && v.Tarikh.Date == kesalahan.Trk.Date);
                                if (originalKesalahanRecord != null)
                                {
                                    kesalahan.Syrt = originalKesalahanRecord.Syrt;
                                    kesalahan.Jab = originalKesalahanRecord.Jab;
                                }
                                _context.tbl_ATTKesalahan.Update(kesalahan);
                                updatedCount++;

                                Console.WriteLine($"🔄 Updated kesalahan: {kesalahan.IdStaff} | {kesalahan.Trk:yyyy-MM-dd} | {kesalahan.JenisKesalahan} | IdSemakan: {kesalahan.IdSemakan}");
                            }
                            await _context.SaveChangesAsync();
                        }
                        else
                        {
                            Console.WriteLine($"ℹ️ Existing Borang A semakan found for current user: {match.IdStaff} | {match.Trk:yyyy-MM-dd} | UpdatedBy: {loggedInUsername} | IdSemakan: {existingSemakan.IdSemakan}");

                            // ✅ HANDLE EXISTING SEMAKAN - UPDATE KESALAHAN RECORDS
                            bool needUpdate = false;
                            if (existingSemakan.Catatan != match.Catatan)
                            {
                                existingSemakan.Catatan = match.Catatan;
                                needUpdate = true;
                            }
                            if (existingSemakan.Name != match.Name)
                            {
                                existingSemakan.Name = match.Name;
                                needUpdate = true;
                            }

                            if (needUpdate)
                            {
                                _context.tbl_ATTSemakanV1.Update(existingSemakan);
                                await _context.SaveChangesAsync();
                                updatedCount++;
                                Console.WriteLine($"🔄 Updated existing Borang A semakan: {match.IdStaff} | {match.Trk:yyyy-MM-dd}");
                            }

                            // Update kesalahan records that don't have this IdSemakan yet
                            var kesalahanRecords = await _context.tbl_ATTKesalahan
                                .Where(k => k.IdStaff == match.IdStaff &&
                                           k.Trk.Date == match.Trk.Date &&
                                            k.IdProfile == SelectedRecordId &&
                                           (k.IdSemakan == null || k.IdSemakan != existingSemakan.IdSemakan))
                                .ToListAsync();

                            Console.WriteLine($"📝 Found {kesalahanRecords.Count} kesalahan records to update for existing semakan {match.IdStaff} on {match.Trk:yyyy-MM-dd}");

                            foreach (var kesalahan in kesalahanRecords)
                            {
                                // ✅ SPECIAL HANDLING FOR "Balik Awal" - NO IdSemakan, Exclude = false
                                if (kesalahan.JenisKesalahan == "Balik Awal")
                                {
                                    kesalahan.IdSemakan = null;
                                    kesalahan.Exclude = null;
                                }
                                else
                                {
                                    kesalahan.IdSemakan = existingSemakan.IdSemakan;
                                    kesalahan.Exclude = true;
                                }

                                // ✅ UPDATE SYRT AND JAB FROM ORIGINAL RECORD
                                var originalKesalahanRecord = await _context.v_HRA_AttKesalahan
                                    .FirstOrDefaultAsync(v => v.idstaff == kesalahan.IdStaff && v.Tarikh.Date == kesalahan.Trk.Date);
                                if (originalKesalahanRecord != null)
                                {
                                    kesalahan.Syrt = originalKesalahanRecord.Syrt;
                                    kesalahan.Jab = originalKesalahanRecord.Jab;
                                }
                                _context.tbl_ATTKesalahan.Update(kesalahan);
                                updatedCount++;

                                Console.WriteLine($"🔄 Updated kesalahan for existing semakan: {kesalahan.IdStaff} | {kesalahan.Trk:yyyy-MM-dd} | {kesalahan.JenisKesalahan} | IdSemakan: {kesalahan.IdSemakan}");
                            }
                            await _context.SaveChangesAsync();
                        }
                    }

                    // === 3. PROCESS BORANG C ===
                    var borangCQuery = from kesalahan in _context.tbl_ATTKesalahan
                                       join borangC in _context.v_HRA_ATTSemakan_BorangC
                                       on new
                                       {
                                           IdStaff = kesalahan.IdStaff,
                                           Tarikh = kesalahan.Trk.Date
                                       }
                                       equals new
                                       {
                                           IdStaff = borangC.nopekerja,
                                           Tarikh = borangC.TrkAduan.Date
                                       }
                                       select new
                                       {
                                           KesalahanId = kesalahan.Id,
                                           kesalahan.IdStaff,
                                           kesalahan.Name,
                                           kesalahan.Trk,
                                           kesalahan.IdSemakan,
                                           AplikasiName = "Borang C",
                                           IdAplikasi = borangC.id,
                                           borangC.Catatan
                                       };

                    // Apply filters
                    if (!string.IsNullOrEmpty(searchIdStaff))
                        borangCQuery = borangCQuery.Where(x => x.IdStaff.Contains(searchIdStaff));
                    if (SelectedUserIds != null && SelectedUserIds.Any())
                        borangCQuery = borangCQuery.Where(x => SelectedUserIds.Contains(x.IdStaff));
                    if (!string.IsNullOrEmpty(searchNama))
                        borangCQuery = borangCQuery.Where(x => x.Name.Contains(searchNama));
                    if (dateFrom.HasValue)
                        borangCQuery = borangCQuery.Where(x => x.Trk.Date >= dateFrom.Value.Date);
                    if (dateTo.HasValue)
                        borangCQuery = borangCQuery.Where(x => x.Trk.Date <= dateTo.Value.Date);

                    var matchResults = await borangCQuery.ToListAsync();
                    Console.WriteLine($"📊 Found {matchResults.Count} Borang C matches");

                    try
                    {
                        foreach (var match in matchResults)
                        {
                            Console.WriteLine($"🔍 Processing: Staff={match.IdStaff}, Date={match.Trk:yyyy-MM-dd}");

                            var existingSemakanForThisUser = await _context.tbl_ATTSemakanV1
                                .FirstOrDefaultAsync(x =>
                                    x.IdStaff == match.IdStaff &&
                                    x.Trk.Date == match.Trk.Date &&
                                    x.IdAplikasi == match.IdAplikasi &&
                                    x.AplikasiName == "Borang C" &&
                                     x.IdProfile == SelectedRecordId &&
                                    x.UpdatedBy == loggedInUsername); // Check if current user already created semakan

                            if (existingSemakanForThisUser == null)
                            {
                                var newSemakan = new tbl_ATTSemakanV1
                                {
                                    IdStaff = match.IdStaff,
                                    Name = match.Name,
                                    Trk = match.Trk,
                                    AplikasiName = match.AplikasiName,
                                    IdAplikasi = match.IdAplikasi,
                                    Catatan = match.Catatan,
                                    UpdatedBy = loggedInUsername,
                                    IdProfile = SelectedRecordId
                                };
                                _context.tbl_ATTSemakanV1.Add(newSemakan);
                                await _context.SaveChangesAsync();
                                insertedBorangCCount++;

                                // Update related kesalahan records
                                var kesalahanRecords = await _context.tbl_ATTKesalahan
                                    .Where(k => k.IdStaff == match.IdStaff &&
                                                k.Trk.Date == match.Trk.Date &&
                                                 k.IdProfile == SelectedRecordId &&
                                                k.IdSemakan == null)
                                    .ToListAsync();

                                Console.WriteLine($"📝 Updating {kesalahanRecords.Count} kesalahan records");

                                foreach (var kesalahan in kesalahanRecords)
                                {
                                    kesalahan.IdSemakan = newSemakan.IdSemakan;
                                    kesalahan.Exclude = true;

                                    var originalKesalahanRecord = await _context.v_HRA_AttKesalahan
                                        .FirstOrDefaultAsync(v => v.idstaff == kesalahan.IdStaff &&
                                                                  v.Tarikh.Date == kesalahan.Trk.Date);
                                    if (originalKesalahanRecord != null)
                                    {
                                        kesalahan.Syrt = originalKesalahanRecord.Syrt;
                                        kesalahan.Jab = originalKesalahanRecord.Jab;
                                    }

                                    _context.tbl_ATTKesalahan.Update(kesalahan);
                                    updatedCount++;
                                }

                                await _context.SaveChangesAsync();
                            }
                            else
                            {
                                Console.WriteLine($"✅ Skipped: semakan by {loggedInUsername} already exists for {match.IdStaff} on {match.Trk:yyyy-MM-dd}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error processing Borang C: {ex.Message}");
                        Console.WriteLine($"📋 Stack trace: {ex.StackTrace}");
                        throw;
                    }

                    // === 4. PROCESS BORANG TUGASAN (Staff Match) ===
                    var allKesalahan = await _context.tbl_ATTKesalahan
                .Where(k =>
          (string.IsNullOrEmpty(searchIdStaff) || k.IdStaff.Contains(searchIdStaff)) &&
          (SelectedUserIds == null || !SelectedUserIds.Any() || SelectedUserIds.Contains(k.IdStaff)))
      .ToListAsync();

                    var allTugasan = await _context.v_HRA_ATTSemakan_BorangTugasan
                        .Where(t =>
                            (string.IsNullOrEmpty(searchIdStaff) || t.NoPekerja.Contains(searchIdStaff)) &&
                            (SelectedUserIds == null || !SelectedUserIds.Any() || SelectedUserIds.Contains(t.NoPekerja)))
                        .ToListAsync();


                    foreach (var tugasan in allTugasan)
                    {
                        var matchedKesalahanList = allKesalahan
                            .Where(k => k.IdStaff.Trim().ToLower() == tugasan.NoPekerja.Trim().ToLower()
                                     && k.Trk.Date >= tugasan.TarikhMula.Date
                                     && k.IdProfile == SelectedRecordId
                                     && k.Trk.Date <= tugasan.TarikhTamat.Date)
                            .ToList();

                        if (matchedKesalahanList.Any())
                        {
                            double bilHari = (double)(tugasan.TarikhTamat - tugasan.TarikhMula).TotalDays + 1;

                            var existingSemakan = await _context.tbl_ATTSemakanV1
    .FirstOrDefaultAsync(s =>
        s.IdStaff.Trim().ToLower() == tugasan.NoPekerja.Trim().ToLower() &&
        s.Trk.Date == tugasan.TarikhMula.Date &&
        s.IdAplikasi == tugasan.IDTugasan &&
         s.IdProfile == SelectedRecordId &&
        s.UpdatedBy == loggedInUsername); // ADD THIS CONDITION

                            if (existingSemakan != null)
                            {
                                // Update existing record logic remains the same
                                existingSemakan.Name = matchedKesalahanList.First().Name;
                                existingSemakan.AplikasiName = "Borang Tugasan";
                                existingSemakan.IdAplikasi = tugasan.IDTugasan;
                                existingSemakan.Catatan = tugasan.Catatan;
                                existingSemakan.TarikhMula = tugasan.TarikhMula;
                                existingSemakan.TarikhTamat = tugasan.TarikhTamat;
                                existingSemakan.BilHari = bilHari;
                                _context.tbl_ATTSemakanV1.Update(existingSemakan);
                            }
                            else
                            {
                                // This will create new record regardless of whether other UpdatedBy exists
                                existingSemakan = new tbl_ATTSemakanV1
                                {
                                    IdStaff = tugasan.NoPekerja,
                                    Name = matchedKesalahanList.First().Name,
                                    Trk = tugasan.TarikhMula,
                                    AplikasiName = "Borang Tugasan",
                                    IdAplikasi = tugasan.IDTugasan,
                                    Catatan = tugasan.Catatan,
                                    TarikhMula = tugasan.TarikhMula,
                                    TarikhTamat = tugasan.TarikhTamat,
                                    BilHari = bilHari,
                                    UpdatedBy = loggedInUsername,
                                    IdProfile = SelectedRecordId
                                };
                                await _context.tbl_ATTSemakanV1.AddAsync(existingSemakan);
                                insertedTugasanCount++;
                            }

                            await _context.SaveChangesAsync();

                            // ✅ UPDATE ALL MATCHING KESALAHAN RECORDS
                            foreach (var kesalahan in matchedKesalahanList)
                            {
                                var kesalahanToUpdate = await _context.tbl_ATTKesalahan
                                    .FirstOrDefaultAsync(k => k.IdStaff == kesalahan.IdStaff &&
                                                             k.Trk.Date == kesalahan.Trk.Date &&
                                                             k.IdSemakan == null);

                                if (kesalahanToUpdate != null)
                                {
                                    kesalahanToUpdate.IdSemakan = existingSemakan.IdSemakan;
                                    kesalahanToUpdate.Exclude = true;
                                    // ✅ UPDATE SYRT AND JAB FROM ORIGINAL RECORD
                                    var originalKesalahanRecord = await _context.v_HRA_AttKesalahan
                                        .FirstOrDefaultAsync(v => v.idstaff == kesalahanToUpdate.IdStaff && v.Tarikh.Date == kesalahanToUpdate.Trk.Date);
                                    if (originalKesalahanRecord != null)
                                    {
                                        kesalahanToUpdate.Syrt = originalKesalahanRecord.Syrt;
                                        kesalahanToUpdate.Jab = originalKesalahanRecord.Jab;
                                    }
                                    _context.tbl_ATTKesalahan.Update(kesalahanToUpdate);
                                    updatedCount++;
                                }
                            }
                            await _context.SaveChangesAsync();
                        }
                    }


                    // === 5. PROCESS CUTI SKOKRAF ===
                    var cutiSkokrafQuery = from kesalahan in _context.tbl_ATTKesalahan
                                           join cutiSkokraf in _context.v_HRA_ATTSemakan_CutiSkokraf
                                           on kesalahan.IdStaff equals cutiSkokraf.KeyCode
                                           join att in _context.v_HRA_AttKesalahan
                                           on new { IdStaff = kesalahan.IdStaff, Tarikh = kesalahan.Trk.Date }
                                           equals new { IdStaff = att.idstaff, Tarikh = att.Tarikh.Date }
                                           // Check if kesalahan.Trk falls within the leave date range
                                           where kesalahan.Trk.Date >= cutiSkokraf.StartDate.Date &&
                                                 kesalahan.Trk.Date <= cutiSkokraf.EndDate.Date
                                           select new
                                           {
                                               KesalahanId = kesalahan.Id,
                                               kesalahan.IdStaff,
                                               kesalahan.Name,
                                               kesalahan.Trk,
                                               kesalahan.IdSemakan,
                                               AplikasiName = "Cuti Skokraf",
                                               IdAplikasi = cutiSkokraf.id,
                                               Catatan = cutiSkokraf.NofDays == 0.5 ? $"Half Day Leave - {cutiSkokraf.Stgh}" : "Full Day Leave",
                                               TarikhMula = cutiSkokraf.StartDate,
                                               TarikhTamat = cutiSkokraf.EndDate,
                                               BilHari = cutiSkokraf.NofDays,

                                               //CUTI PAGI , KERJA PETANG
                                               // ValInNew logic - automatic calculation based on roster time
                                               ValInNew = cutiSkokraf.NofDays == 0.5
    ? (cutiSkokraf.Stgh == "Pagi"
        ? att.valIn + 300  // Start after lunch (5 hours later)
        : att.valIn)       // Start normal time
    : att.valIn,


                                               //CUTI PETANG, KERJA PAGI
                                               // ValOutNew logic - automatic calculation based on roster time  
                                               ValOutNew = cutiSkokraf.NofDays == 0.5
                                                   ? (cutiSkokraf.Stgh == "Petang"
                                                       ? att.valIn + 240  // Add 4 hours from start time for afternoon leave
                                                       : att.valOut)      // Keep original time for morning leave
                                                   : att.valOut,

                                               ACTValInNew = att.ACTValIn,
                                               ACTValOutNew = att.ACTValOut,
                                               // Include staff details for penalty records
                                               Syrt = att.Syrt,
                                               Jab = att.Jab,
                                               Bhgn = att.Bhgn,
                                               Jaw = att.Jaw
                                           };

                    cutiSkokrafQuery = cutiSkokrafQuery.Where(x =>
                    (string.IsNullOrEmpty(searchIdStaff) || x.IdStaff.Contains(searchIdStaff)) &&
                    (SelectedUserIds == null || !SelectedUserIds.Any() || SelectedUserIds.Contains(x.IdStaff))
                    );


                    if (!string.IsNullOrEmpty(searchIdStaff))
                        cutiSkokrafQuery = cutiSkokrafQuery.Where(x => x.IdStaff.Contains(searchIdStaff));

                    if (!string.IsNullOrEmpty(searchNama))
                        cutiSkokrafQuery = cutiSkokrafQuery.Where(x => x.Name.Contains(searchNama));
                    if (dateFrom.HasValue)
                        cutiSkokrafQuery = cutiSkokrafQuery.Where(x => x.Trk.Date >= dateFrom.Value.Date);
                    if (dateTo.HasValue)
                        cutiSkokrafQuery = cutiSkokrafQuery.Where(x => x.Trk.Date <= dateTo.Value.Date);

                    // ✅ IMPORTANT: Group by leave application AND individual date to handle multi-day leaves properly
                    var distinctMatches = await cutiSkokrafQuery
                       .GroupBy(x => new { x.IdStaff, x.TarikhMula, x.TarikhTamat, x.AplikasiName, x.Trk.Date }) // Added Trk.Date
                       .Select(g => g.First())
                       .ToListAsync();

                    foreach (var match in distinctMatches)
                    {
                        // ✅ MODIFIED: Check for semakan record for this specific date within the leave period
                        var existingSemakan = await _context.tbl_ATTSemakanV1
                            .FirstOrDefaultAsync(x =>
                                x.IdStaff == match.IdStaff &&
                                x.TarikhMula.HasValue && x.TarikhTamat.HasValue &&
                                x.TarikhMula.Value.Date == match.TarikhMula.Date &&
                                x.TarikhTamat.Value.Date == match.TarikhTamat.Date &&
                                x.AplikasiName == "Cuti Skokraf" &&
                                 x.IdProfile == SelectedRecordId &&
                                x.UpdatedBy == loggedInUsername);

                        tbl_ATTSemakanV1 semakanRecord;
                        if (existingSemakan == null)
                        {
                            // Create new semakan record (only once per leave application)
                            semakanRecord = new tbl_ATTSemakanV1
                            {
                                IdStaff = match.IdStaff,
                                Name = match.Name,
                                Trk = match.Trk, // This will be the current processing date
                                AplikasiName = match.AplikasiName,
                                IdAplikasi = match.IdAplikasi,
                                Catatan = match.Catatan,
                                TarikhMula = match.TarikhMula,
                                TarikhTamat = match.TarikhTamat,
                                BilHari = match.BilHari,
                                valInNEW = match.ValInNew,
                                valOutNEW = match.ValOutNew,
                                ACTValInNEW = match.ACTValInNew,
                                ACTValOutNEW = match.ACTValOutNew,
                                UpdatedBy = loggedInUsername,
                                IdProfile = SelectedRecordId
                            };

                            _context.tbl_ATTSemakanV1.Add(semakanRecord);
                            await _context.SaveChangesAsync();
                            insertedCutiSkokrafCount++;

                            System.Diagnostics.Debug.WriteLine($"✅ Created new semakan record: {match.IdStaff} | {match.TarikhMula:yyyy-MM-dd} to {match.TarikhTamat:yyyy-MM-dd} | Processing Date: {match.Trk:yyyy-MM-dd} | IdAplikasi: {match.IdAplikasi}");
                        }
                        else
                        {
                            // Update existing semakan record if needed
                            semakanRecord = existingSemakan;
                            bool needUpdate = false;

                            if (existingSemakan.IdAplikasi != match.IdAplikasi)
                            {
                                existingSemakan.IdAplikasi = match.IdAplikasi;
                                needUpdate = true;
                            }
                            if (existingSemakan.Catatan != match.Catatan)
                            {
                                existingSemakan.Catatan = match.Catatan;
                                needUpdate = true;
                            }
                            if (existingSemakan.Name != match.Name)
                            {
                                existingSemakan.Name = match.Name;
                                needUpdate = true;
                            }
                            if (existingSemakan.valInNEW != match.ValInNew)
                            {
                                existingSemakan.valInNEW = match.ValInNew;
                                needUpdate = true;
                            }
                            if (existingSemakan.valOutNEW != match.ValOutNew)
                            {
                                existingSemakan.valOutNEW = match.ValOutNew;
                                needUpdate = true;
                            }
                            if (existingSemakan.ACTValInNEW != match.ACTValInNew)
                            {
                                existingSemakan.ACTValInNEW = match.ACTValInNew;
                                needUpdate = true;
                            }
                            if (existingSemakan.ACTValOutNEW != match.ACTValOutNew)
                            {
                                existingSemakan.ACTValOutNEW = match.ACTValOutNew;
                                needUpdate = true;
                            }

                            if (needUpdate)
                            {
                                _context.tbl_ATTSemakanV1.Update(existingSemakan);
                                await _context.SaveChangesAsync();
                                System.Diagnostics.Debug.WriteLine($"🔄 Updated existing semakan record: {match.IdStaff} | {match.TarikhMula:yyyy-MM-dd} | IdAplikasi: {match.IdAplikasi}");
                            }
                        }

                        // ✅ NEW LOGIC: CHECK FOR DUPLICATE DATES AND HANDLE SPECIFIC VIOLATIONS
                        // Check if there are multiple Cuti Skokraf records for the same date
                        // ✅ MODIFIED: Check for duplicate dates for this specific processing date
                        var duplicateCutiRecords = await _context.v_HRA_ATTSemakan_CutiSkokraf
                            .Where(c => c.KeyCode == match.IdStaff &&
                                       c.StartDate.Date <= match.Trk.Date &&
                                       c.EndDate.Date >= match.Trk.Date) // Check if current date falls within any leave period
                            .CountAsync();

                        bool hasDuplicateDate = duplicateCutiRecords > 1;

                        if (hasDuplicateDate)
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠️ Duplicate date detected for {match.IdStaff} on {match.Trk:yyyy-MM-dd}");
                        }

                        var kesalahanRecords = await _context.tbl_ATTKesalahan
       .Where(k => k.IdStaff == match.IdStaff &&
                  k.Trk.Date == match.Trk.Date && // Process for the specific date
                  (k.IdSemakan == null || k.IdSemakan != semakanRecord.IdSemakan) &&
                  k.IdProfile == SelectedRecordId) // ✅ ADD THIS LINE
       .ToListAsync();

                        foreach (var kesalahan in kesalahanRecords)
                        {
                            // ✅ SPECIAL HANDLING FOR DUPLICATE DATES
                            if (hasDuplicateDate && (kesalahan.JenisKesalahan == "Datang Lewat" || kesalahan.JenisKesalahan == "Balik Awal"))
                            {
                                // For duplicate dates with "Datang Lewat" or "Balik Awal", do NOT add IdSemakan and set Exclude to False
                                kesalahan.IdSemakan = null;
                                kesalahan.Exclude = null;

                                System.Diagnostics.Debug.WriteLine($"🔄 Duplicate date - NOT linking kesalahan: {kesalahan.IdStaff} | {kesalahan.Trk:yyyy-MM-dd} | {kesalahan.JenisKesalahan} | IdSemakan: NULL | Exclude: False");
                            }
                            else
                            {
                                kesalahan.IdSemakan = semakanRecord.IdSemakan;
                                kesalahan.Exclude = true;

                                System.Diagnostics.Debug.WriteLine($"🔄 Normal processing - linking kesalahan: {kesalahan.IdStaff} | {kesalahan.Trk:yyyy-MM-dd} | {kesalahan.JenisKesalahan} | IdSemakan: {kesalahan.IdSemakan} | Exclude: True");
                            }

                            // Update staff details if missing
                            if (string.IsNullOrEmpty(kesalahan.Syrt)) kesalahan.Syrt = match.Syrt ?? "";
                            if (string.IsNullOrEmpty(kesalahan.Jab)) kesalahan.Jab = match.Jab ?? "";
                            if (string.IsNullOrEmpty(kesalahan.Bhgn)) kesalahan.Bhgn = match.Bhgn ?? "";
                            if (string.IsNullOrEmpty(kesalahan.Jaw)) kesalahan.Jaw = match.Jaw ?? "";

                            _context.tbl_ATTKesalahan.Update(kesalahan);
                            updatedCount++;
                        }
                        if (kesalahanRecords.Any())
                        {
                            await _context.SaveChangesAsync();
                            System.Diagnostics.Debug.WriteLine($"✅ Updated {kesalahanRecords.Count} kesalahan records for {match.IdStaff} on {match.Trk:yyyy-MM-dd}");
                        }

                        // ✅ CHECK IF ATTENDANCE IS BETTER THAN EXPECTED (SHOULD REMOVE RECORDS) - Only if NOT duplicate date
                        bool shouldRemoveRecord = false;

                        if (!hasDuplicateDate && match.ACTValInNew.HasValue && match.ACTValOutNew.HasValue &&
                            match.ValInNew.HasValue && match.ValOutNew.HasValue)
                        {
                            // Check if came earlier than expected AND left later than expected
                            bool cameEarlier = match.ACTValInNew.Value < match.ValInNew.Value;
                            bool leftLater = match.ACTValOutNew.Value > match.ValOutNew.Value;

                            if (cameEarlier && leftLater)
                            {
                                shouldRemoveRecord = true;
                                System.Diagnostics.Debug.WriteLine($"🧹 Staff performed better than expected - will remove records: {match.IdStaff} | {match.Trk:yyyy-MM-dd}");
                                System.Diagnostics.Debug.WriteLine($"   Expected: In={match.ValInNew}, Out={match.ValOutNew}");
                                System.Diagnostics.Debug.WriteLine($"   Actual: In={match.ACTValInNew}, Out={match.ACTValOutNew}");
                            }
                        }

                        if (shouldRemoveRecord)
                        {
                            // ✅ REMOVE KESALAHAN RECORDS FOR THIS STAFF AND DATE
                            var kesalahanToRemove = await _context.tbl_ATTKesalahan
      .Where(k => k.IdStaff == match.IdStaff &&
                 k.Trk.Date == match.Trk.Date &&
                 k.IdSemakan == semakanRecord.IdSemakan &&
                 k.IdProfile == SelectedRecordId) // ✅ ADD THIS LINE
      .ToListAsync();

                            if (kesalahanToRemove.Any())
                            {
                                _context.tbl_ATTKesalahan.RemoveRange(kesalahanToRemove);
                                await _context.SaveChangesAsync();
                                System.Diagnostics.Debug.WriteLine($"🗑️ Removed {kesalahanToRemove.Count} kesalahan records for {match.IdStaff} on {match.Trk:yyyy-MM-dd}");
                            }

                            // ✅ REMOVE SEMAKAN RECORD
                            _context.tbl_ATTSemakanV1.Remove(semakanRecord);
                            await _context.SaveChangesAsync();
                            System.Diagnostics.Debug.WriteLine($"🗑️ Removed semakan record for {match.IdStaff} on {match.Trk:yyyy-MM-dd}");
                        }
                        else
                        {
                            // ✅ ENHANCED PENALTY RECORDS CREATION WITH PONTENG SEPARUH HARI DETECTION
                            if (!hasDuplicateDate && match.BilHari == 0.5 && match.ACTValInNew.HasValue && match.ACTValOutNew.HasValue &&
                                match.ValInNew.HasValue && match.ValOutNew.HasValue)
                            {
                                var penaltyRecords = new List<tbl_ATTKesalahan>();

                                // ✅ SPECIAL CASE: Check if came earlier AND left later (good performance)
                                bool cameEarlier = match.ACTValInNew.Value < match.ValInNew.Value;
                                bool leftLater = match.ACTValOutNew.Value > match.ValOutNew.Value;

                                if (cameEarlier && leftLater)
                                {
                                    // Staff performed better than expected - keep IdSemakan and set Exclude = true
                                    System.Diagnostics.Debug.WriteLine($"✅ Staff performed better than expected: {match.IdStaff} | {match.Trk:yyyy-MM-dd} - keeping IdSemakan and setting Exclude = true");

                                    // Update existing kesalahan records to keep IdSemakan and set Exclude = true
                                    var existingKesalahan = await _context.tbl_ATTKesalahan
     .Where(k => k.IdStaff == match.IdStaff &&
                k.Trk.Date == match.Trk.Date &&
                (k.IdSemakan == null || k.IdSemakan != semakanRecord.IdSemakan) &&
                k.IdProfile == SelectedRecordId) // ✅ ADD THIS LINE
     .ToListAsync();


                                    foreach (var kesalahan in existingKesalahan)
                                    {
                                        kesalahan.IdSemakan = semakanRecord.IdSemakan;
                                        kesalahan.Exclude = true;
                                        _context.tbl_ATTKesalahan.Update(kesalahan);
                                    }

                                    if (existingKesalahan.Any())
                                    {
                                        await _context.SaveChangesAsync();
                                    }
                                }
                                else
                                {
                                    // ✅ CHECK FOR MORNING VIOLATIONS (LATE ARRIVAL)
                                    if (match.ACTValInNew > match.ValInNew)
                                    {
                                        var violation = match.ACTValInNew.Value - match.ValInNew.Value;
                                        string jenisKesalahan;

                                        // Determine violation type based on duration
                                        if (violation > 120) // More than 2 hours
                                        {
                                            jenisKesalahan = "Ponteng Separuh Hari (TimeIn)";
                                        }
                                        else
                                        {
                                            jenisKesalahan = "Datang Lewat";
                                        }

                                        // Check if penalty record already exists
                                        var existingPenalty = await _context.tbl_ATTKesalahan
     .FirstOrDefaultAsync(k => k.IdStaff == match.IdStaff &&
                              k.Trk.Date == match.Trk.Date &&
                              k.JenisKesalahan == jenisKesalahan &&
                              k.IdSemakan == semakanRecord.IdSemakan &&
                              k.IdProfile == SelectedRecordId);

                                        if (existingPenalty == null)
                                        {
                                            penaltyRecords.Add(new tbl_ATTKesalahan
                                            {
                                                IdStaff = match.IdStaff,
                                                Name = match.Name,
                                                Trk = match.Trk,
                                                JenisKesalahan = jenisKesalahan,
                                                Exclude = null, // Set to null for penalty records
                                                IdSemakan = semakanRecord.IdSemakan,
                                                Syrt = match.Syrt ?? "",
                                                Jab = match.Jab ?? "",
                                                Bhgn = match.Bhgn ?? "",
                                                Jaw = match.Jaw ?? "",
                                                IdProfile = SelectedRecordId
                                            });

                                            System.Diagnostics.Debug.WriteLine($"🚨 Will create penalty record: {match.IdStaff} | {match.Trk:yyyy-MM-dd} | {jenisKesalahan} | Violation: {violation} minutes");
                                        }
                                    }

                                    // ✅ CHECK FOR EVENING VIOLATIONS (EARLY DEPARTURE)
                                    if (match.ACTValOutNew < match.ValOutNew)
                                    {
                                        var violation = match.ValOutNew.Value - match.ACTValOutNew.Value;
                                        string jenisKesalahan;

                                        // Determine violation type based on duration
                                        if (violation > 120) // More than 2 hours
                                        {
                                            jenisKesalahan = "Ponteng Separuh Hari (TimeOut)";
                                        }
                                        else
                                        {
                                            jenisKesalahan = "Balik Awal";
                                        }

                                        // Check if penalty record already exists
                                        var existingPenalty = await _context.tbl_ATTKesalahan
      .FirstOrDefaultAsync(k => k.IdStaff == match.IdStaff &&
                               k.Trk.Date == match.Trk.Date &&
                               k.JenisKesalahan == jenisKesalahan &&
                               k.IdSemakan == semakanRecord.IdSemakan &&
                               k.IdProfile == SelectedRecordId); 

                                        if (existingPenalty == null)
                                        {
                                            penaltyRecords.Add(new tbl_ATTKesalahan
                                            {
                                                IdStaff = match.IdStaff,
                                                Name = match.Name,
                                                Trk = match.Trk,
                                                JenisKesalahan = jenisKesalahan,
                                                Exclude = null, // Set to null for penalty records
                                                IdSemakan = semakanRecord.IdSemakan,
                                                Syrt = match.Syrt ?? "",
                                                Jab = match.Jab ?? "",
                                                Bhgn = match.Bhgn ?? "",
                                                Jaw = match.Jaw ?? "",
                                                IdProfile = SelectedRecordId
                                            });

                                            System.Diagnostics.Debug.WriteLine($"🚨 Will create penalty record: {match.IdStaff} | {match.Trk:yyyy-MM-dd} | {jenisKesalahan} | Violation: {violation} minutes");
                                        }
                                    }

                                    // Add penalty records if any
                                    if (penaltyRecords.Any())
                                    {
                                        _context.tbl_ATTKesalahan.AddRange(penaltyRecords);
                                        await _context.SaveChangesAsync();

                                        System.Diagnostics.Debug.WriteLine($"✅ Created {penaltyRecords.Count} penalty records for {match.IdStaff} on {match.Trk:yyyy-MM-dd}");
                                    }
                                }
                            }
                            else if (match.BilHari == 1.0)
                            {
                                // For full day leave, no penalty records should be created
                                System.Diagnostics.Debug.WriteLine($"ℹ️ Full day leave - no penalty calculation needed: {match.IdStaff} | {match.Trk:yyyy-MM-dd}");
                            }
                            else if (hasDuplicateDate)
                            {
                                System.Diagnostics.Debug.WriteLine($"ℹ️ Duplicate date detected - penalty calculation skipped: {match.IdStaff} | {match.Trk:yyyy-MM-dd}");
                            }
                        }
                    }

                    // ✅ ENHANCED LOGIC FOR EXISTING "Ponteng Separuh Hari" RECORDS - NEW SCENARIO
                    var pontengSeparuhHariRecords = await _context.tbl_ATTKesalahan
      .Where(k => (k.JenisKesalahan == "Ponteng Separuh Hari (TimeIn)" ||
                   k.JenisKesalahan == "Ponteng Separuh Hari (TimeOut)") &&
                  k.IdProfile == SelectedRecordId) // ✅ ADD THIS LINE
      .ToListAsync();

                    foreach (var kesalahan in pontengSeparuhHariRecords)
                    {
                        var semakan = await _context.tbl_ATTSemakanV1
        .FirstOrDefaultAsync(s => s.IdSemakan == kesalahan.IdSemakan &&
                                 s.IdProfile == SelectedRecordId);

                        if (semakan == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠️ Skipping Ponteng Separuh Hari record with no matching semakan: {kesalahan.IdStaff} | {kesalahan.Trk:yyyy-MM-dd}");
                            continue;
                        }

                        var newKesalahanRecords = new List<tbl_ATTKesalahan>();

                        // ✅ CHECK FOR PONTENG SEPARUH HARI (TIMEIN) VIOLATIONS
                        if (kesalahan.JenisKesalahan == "Ponteng Separuh Hari (TimeIn)" &&
                            semakan.ACTValInNEW.HasValue && semakan.valInNEW.HasValue)
                        {
                            if (semakan.ACTValInNEW.Value > semakan.valInNEW.Value)
                            {
                                var violation = semakan.ACTValInNEW.Value - semakan.valInNEW.Value;

                                if (violation <= 120) // 2 hours or less
                                {
                                    // Keep the original record but update it
                                    kesalahan.IdSemakan = semakan.IdSemakan; // Keep IdSemakan
                                    kesalahan.Exclude = true; // Set Exclude = True

                                    // Create new "Datang Lewat" record
                                    var datangLewatRecord = new tbl_ATTKesalahan
                                    {
                                        IdStaff = kesalahan.IdStaff,
                                        Name = kesalahan.Name,
                                        Trk = kesalahan.Trk,
                                        JenisKesalahan = "Datang Lewat",
                                        Exclude = null, // New violation record
                                        IdSemakan = null, // No semakan link for new violation
                                        Syrt = kesalahan.Syrt ?? "",
                                        Jab = kesalahan.Jab ?? "",
                                        Bhgn = kesalahan.Bhgn ?? "",
                                        Jaw = kesalahan.Jaw ?? "",
                                        IdProfile = SelectedRecordId

                                    };

                                    newKesalahanRecords.Add(datangLewatRecord);

                                    System.Diagnostics.Debug.WriteLine($"🔄 Keeping Ponteng Separuh Hari (TimeIn) with IdSemakan and Exclude=True, adding Datang Lewat record: {kesalahan.IdStaff} | {kesalahan.Trk:yyyy-MM-dd} | Violation: {violation} minutes");
                                }
                                // If violation > 120, keep as "Ponteng Separuh Hari (TimeIn)" with existing IdSemakan and Exclude
                            }
                            else
                            {
                                // Actual time is better than expected, keep IdSemakan and set Exclude = True
                                kesalahan.IdSemakan = semakan.IdSemakan;
                                kesalahan.Exclude = true;
                                System.Diagnostics.Debug.WriteLine($"🔄 Keeping IdSemakan and setting Exclude=True for improved performance: {kesalahan.IdStaff} | {kesalahan.Trk:yyyy-MM-dd} | {kesalahan.JenisKesalahan}");
                            }
                        }

                        // ✅ CHECK FOR PONTENG SEPARUH HARI (TIMEOUT) VIOLATIONS
                        if (kesalahan.JenisKesalahan == "Ponteng Separuh Hari (TimeOut)" &&
                            semakan.ACTValOutNEW.HasValue && semakan.valOutNEW.HasValue)
                        {
                            if (semakan.ACTValOutNEW.Value < semakan.valOutNEW.Value)
                            {
                                var violation = semakan.valOutNEW.Value - semakan.ACTValOutNEW.Value;

                                if (violation <= 120) // 2 hours or less
                                {
                                    // Keep the original record but update it
                                    kesalahan.IdSemakan = semakan.IdSemakan; // Keep IdSemakan
                                    kesalahan.Exclude = true; // Set Exclude = True

                                    // Create new "Balik Awal" record
                                    var balikAwalRecord = new tbl_ATTKesalahan
                                    {
                                        IdStaff = kesalahan.IdStaff,
                                        Name = kesalahan.Name,
                                        Trk = kesalahan.Trk,
                                        JenisKesalahan = "Balik Awal",
                                        Exclude = null, // New violation record
                                        IdSemakan = null, // No semakan link for new violation
                                        Syrt = kesalahan.Syrt ?? "",
                                        Jab = kesalahan.Jab ?? "",
                                        Bhgn = kesalahan.Bhgn ?? "",
                                        Jaw = kesalahan.Jaw ?? "",
                                        IdProfile = SelectedRecordId
                                    };

                                    newKesalahanRecords.Add(balikAwalRecord);

                                    System.Diagnostics.Debug.WriteLine($"🔄 Keeping Ponteng Separuh Hari (TimeOut) with IdSemakan and Exclude=True, adding Balik Awal record: {kesalahan.IdStaff} | {kesalahan.Trk:yyyy-MM-dd} | Violation: {violation} minutes");
                                }
                                // If violation > 120, keep as "Ponteng Separuh Hari (TimeOut)" with existing IdSemakan and Exclude
                            }
                            else
                            {
                                // Actual time is better than expected, keep IdSemakan and set Exclude = True
                                kesalahan.IdSemakan = semakan.IdSemakan;
                                kesalahan.Exclude = true;
                                System.Diagnostics.Debug.WriteLine($"🔄 Keeping IdSemakan and setting Exclude=True for improved performance: {kesalahan.IdStaff} | {kesalahan.Trk:yyyy-MM-dd} | {kesalahan.JenisKesalahan}");
                            }
                        }

                        // Update the original record
                        _context.tbl_ATTKesalahan.Update(kesalahan);

                        // Add new kesalahan records if any
                        if (newKesalahanRecords.Any())
                        {
                            _context.tbl_ATTKesalahan.AddRange(newKesalahanRecords);
                            System.Diagnostics.Debug.WriteLine($"✅ Added {newKesalahanRecords.Count} new kesalahan records for {kesalahan.IdStaff} on {kesalahan.Trk:yyyy-MM-dd}");
                        }

                        await _context.SaveChangesAsync();
                    }

                    System.Diagnostics.Debug.WriteLine($"📊 Cuti Skokraf Processing Summary:");
                    System.Diagnostics.Debug.WriteLine($"   - New semakan records created: {insertedCutiSkokrafCount}");
                    System.Diagnostics.Debug.WriteLine($"   - Kesalahan records updated: {updatedCount}");

                    await transaction.CommitAsync();

                    //=== BUILD MESSAGE ===
                    var messageParts = new List<string>();
                    if (insertedCutiCount > 0)
                        messageParts.Add($"Cuti Umum: {insertedCutiCount} inserted");
                    if (insertedBorangACount > 0)
                        messageParts.Add($"Borang A: {insertedBorangACount} inserted");
                    if (insertedBorangCCount > 0)
                        messageParts.Add($"Borang C: {insertedBorangCCount} inserted");
                    if (insertedCutiSkokrafCount > 0)
                        messageParts.Add($"Cuti Skokraf: {insertedCutiSkokrafCount} inserted");
                    if (insertedTugasanCount > 0)
                        messageParts.Add($"Borang Tugasan: {insertedTugasanCount} inserted");
                    if (updatedCount > 0)
                        messageParts.Add($"{updatedCount} records updated");

                    TempData["Message"] = messageParts.Any() ? string.Join(". ", messageParts) + "." : "No new records to process.";
                    TempData["ActiveTab"] = activeTab;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    TempData["Error"] = $"Error occurred during combined processing: {ex.Message}";
                    TempData["ActiveTab"] = activeTab;
                }
            }

            //TempData["FromProsesSemakan"] = true;
            return RedirectToAction("Search", "AttKesalahan", new
            {
                searchIdStaff = searchIdStaff,
                searchNama = searchNama,
                dateFrom = dateFrom?.ToString("yyyy-MM-dd"),
                dateTo = dateTo?.ToString("yyyy-MM-dd"),
                activeTab = activeTab,
                SelectedUserIds = SelectedUserIds,
                UpdatedBy = loggedInUsername,
                id = SelectedRecordId,
                skipDeletion = true  // 🆕 Skip deletion when coming from ProcessSemakan
            });
        }
    }

}
