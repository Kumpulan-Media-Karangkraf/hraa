using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using HRAnalysis.Models;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HRAnalysis.Data;
using System.Text;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

public class ExportController : Controller
{
    private readonly AppDbContext _context;

    public ExportController(AppDbContext context)
    {
        _context = context;
    }


    [HttpPost]
    public async Task<IActionResult> ExportToExcelHtml(string searchIdStaff = "", string searchNama = "",
    string searchSyrt = "", string searchJab = "", string searchBhgn = "", string searchJaw = "",
    string dateFrom = "", string dateTo = "", string searchSelectedNama = "", int? SelectedRecordId = null,
    List<string> SelectedUserIds = null)
    {
        try
        {
            // Parse dates
            DateTime? parsedDateFrom = null;
            DateTime? parsedDateTo = null;

            if (DateTime.TryParse(dateFrom, out DateTime tempDateFrom))
                parsedDateFrom = tempDateFrom;

            if (DateTime.TryParse(dateTo, out DateTime tempDateTo))
                parsedDateTo = tempDateTo;

            // Check if any search filters are applied
            bool hasSearchFilters = !string.IsNullOrEmpty(searchIdStaff) ||
                                   !string.IsNullOrEmpty(searchNama) ||
                                   !string.IsNullOrEmpty(searchSyrt) ||
                                   !string.IsNullOrEmpty(searchJab) ||
                                   !string.IsNullOrEmpty(searchBhgn) ||
                                   !string.IsNullOrEmpty(searchJaw) ||
                                   !string.IsNullOrEmpty(searchSelectedNama) ||
                                   (SelectedUserIds != null && SelectedUserIds.Any()) ||
                                   parsedDateFrom.HasValue ||
                                   parsedDateTo.HasValue;

            // Build query with same filters as Search method
            var query = _context.tbl_ATTKesalahan.AsQueryable();

            // If SelectedRecordId is provided, filter by it
            if (SelectedRecordId.HasValue)
            {
                query = query.Where(x => x.IdProfile == SelectedRecordId.Value);
            }

            // Apply search filters only if they exist
            if (hasSearchFilters)
            {
                if (!string.IsNullOrEmpty(searchIdStaff))
                {
                    query = query.Where(x => x.IdStaff != null && x.IdStaff.Contains(searchIdStaff));
                }

                if (!string.IsNullOrEmpty(searchNama))
                {
                    query = query.Where(x => x.Name != null && x.Name.Contains(searchNama));
                }

                // Apply multi-select filter
                if (SelectedUserIds != null && SelectedUserIds.Any())
                {
                    query = query.Where(x => SelectedUserIds.Contains(x.IdStaff));
                }
                else if (!string.IsNullOrEmpty(searchSelectedNama))
                {
                    query = query.Where(x => x.IdStaff == searchSelectedNama);
                }

                if (!string.IsNullOrEmpty(searchSyrt))
                {
                    query = query.Where(x => x.Syrt != null && x.Syrt.Contains(searchSyrt));
                }

                if (!string.IsNullOrEmpty(searchJab))
                {
                    query = query.Where(x => x.Jab != null && x.Jab.Contains(searchJab));
                }

                if (!string.IsNullOrEmpty(searchBhgn))
                {
                    query = query.Where(x => x.Bhgn != null && x.Bhgn.Contains(searchBhgn));
                }

                if (!string.IsNullOrEmpty(searchJaw))
                {
                    query = query.Where(x => x.Jaw != null && x.Jaw.Contains(searchJaw));
                }

                if (parsedDateFrom.HasValue)
                {
                    query = query.Where(x => x.Trk.Date >= parsedDateFrom.Value.Date);
                }

                if (parsedDateTo.HasValue)
                {
                    query = query.Where(x => x.Trk.Date <= parsedDateTo.Value.Date);
                }
            }

            // Exclude "Hari Tidak Bekerja" records
            query = query.Where(x => x.JenisKesalahan != "Hari Tidak Bekerja");

            var kesalahanRecords = await query.ToListAsync();

            // Determine report type and title
            string reportType = hasSearchFilters ? "FILTERED" : "EXISTING RECORDS";
            string titleSuffix = SelectedRecordId.HasValue ? $" - Profile ID: {SelectedRecordId.Value}" : "";
            string filterInfo = hasSearchFilters ?
                $"({parsedDateFrom?.ToString("dd/MM/yyyy") ?? "Tidak ditetapkan"}) SEHINGGA ({parsedDateTo?.ToString("dd/MM/yyyy") ?? "Tidak ditetapkan"})" :
                "(ALL EXISTING RECORDS)";

            // Group by staff and count different types of kesalahan
            var staffSummary = kesalahanRecords
                .GroupBy(x => new { x.IdStaff, x.Name, x.Jaw, x.Jab, x.Bhgn, x.Syrt })
                .Select(g => new
                {
                    IdStaff = g.Key.IdStaff,
                    Name = g.Key.Name,
                    Jawatan = g.Key.Jaw,
                    Jabatan = g.Key.Jab,
                    Bahagian = g.Key.Bhgn,
                    Syarikat = g.Key.Syrt,

                    // Filter condition based on whether search filters are applied
                    TiadaKenyataan = hasSearchFilters ?
                        g.Count(x => x.IdSemakan == null && x.Exclude == null) :
                        g.Count(),
                    DatangLewat = hasSearchFilters ?
                        g.Count(x => x.IdSemakan == null && x.Exclude == null && x.JenisKesalahan == "Datang Lewat") :
                        g.Count(x => x.JenisKesalahan == "Datang Lewat"),
                    BalikCepat = hasSearchFilters ?
                        g.Count(x => x.IdSemakan == null && x.Exclude == null && x.JenisKesalahan == "Balik Awal") :
                        g.Count(x => x.JenisKesalahan == "Balik Awal"),
                    TidakPunchIn = hasSearchFilters ?
                        g.Count(x => x.IdSemakan == null && x.Exclude == null && x.JenisKesalahan == "Tiada TimeIn") :
                        g.Count(x => x.JenisKesalahan == "Tiada TimeIn"),
                    TidakPunchOut = hasSearchFilters ?
                        g.Count(x => x.IdSemakan == null && x.Exclude == null && x.JenisKesalahan == "Tiada TimeOut") :
                        g.Count(x => x.JenisKesalahan == "Tiada TimeOut"),
                    Ponteng = hasSearchFilters ?
                        g.Count(x => x.IdSemakan == null && x.Exclude == null && x.JenisKesalahan == "Ponteng") :
                        g.Count(x => x.JenisKesalahan == "Ponteng"),
                    PontengSeparuhHariTimeIn = hasSearchFilters ?
                        g.Count(x => x.IdSemakan == null && x.Exclude == null && x.JenisKesalahan == "Ponteng Separuh Hari (TimeIn)") :
                        g.Count(x => x.JenisKesalahan == "Ponteng Separuh Hari (TimeIn)"),
                    PontengSeparuhHariTimeOut = hasSearchFilters ?
                        g.Count(x => x.IdSemakan == null && x.Exclude == null && x.JenisKesalahan == "Ponteng Separuh Hari (TimeOut)") :
                        g.Count(x => x.JenisKesalahan == "Ponteng Separuh Hari (TimeOut)"),

                    // Total should be the sum of all the above categories
                    TotalKesalahan = hasSearchFilters ?
                        g.Count(x => x.IdSemakan == null && x.Exclude == null) :
                        g.Count()
                })
                .OrderBy(x => x.IdStaff)
                .ToList();

            // Generate HTML table
            var sb = new StringBuilder();

            sb.AppendLine("<table border='1' style='border-collapse: collapse; font-family: Arial, sans-serif; width: 100%;'>");

            // Title row - spans across all columns (6 basic + 8 problem types + 1 total = 15 columns)
            sb.AppendLine($"<tr><td colspan='15' align='center' style='font-weight: bold; padding: 10px; background-color: #f0f0f0;'><b>ANALISIS REKOD KEHADIRAN STAFF {filterInfo}{titleSuffix}</b></td></tr>");

            // Add note about report type
            if (!hasSearchFilters)
            {
                sb.AppendLine($"<tr><td colspan='15' align='center' style='padding: 5px; background-color: #fff3cd; font-style: italic;'>Note: This report shows all existing records without filters</td></tr>");
            }

            // Header rows - First header row
            sb.AppendLine("<tr style='background-color: #e0e0e0;'>");
            sb.AppendLine("<th rowspan='2' style='padding: 8px; text-align: center; vertical-align: middle;'>Bil</th>");
            sb.AppendLine("<th rowspan='2' style='padding: 8px; text-align: center; vertical-align: middle;'>No Pekerja</th>");
            sb.AppendLine("<th rowspan='2' style='padding: 8px; text-align: center; vertical-align: middle;'>Nama</th>");
            sb.AppendLine("<th rowspan='2' style='padding: 8px; text-align: center; vertical-align: middle;'>Jawatan</th>");
            sb.AppendLine("<th rowspan='2' style='padding: 8px; text-align: center; vertical-align: middle;'>Jabatan</th>");
            sb.AppendLine("<th rowspan='2' style='padding: 8px; text-align: center; vertical-align: middle;'>Bahagian</th>");
            sb.AppendLine("<th colspan='8' style='padding: 8px; text-align: center; background-color: #d0d0d0;'>MASALAH KEHADIRAN</th>");
            sb.AppendLine("<th rowspan='2' style='padding: 8px; text-align: center; vertical-align: middle; background-color: #ffcccc;'>Total</th>");
            sb.AppendLine("</tr>");

            // Second header row - Sub-categories
            sb.AppendLine("<tr style='background-color: #e0e0e0;'>");
            sb.AppendLine("<th style='padding: 8px; text-align: center; min-width: 80px;'>Tiada Kenyataan</th>");
            sb.AppendLine("<th style='padding: 8px; text-align: center; min-width: 80px;'>Datang Lewat</th>");
            sb.AppendLine("<th style='padding: 8px; text-align: center; min-width: 80px;'>Balik Awal</th>");
            sb.AppendLine("<th style='padding: 8px; text-align: center; min-width: 80px;'>Tiada TimeIn</th>");
            sb.AppendLine("<th style='padding: 8px; text-align: center; min-width: 80px;'>Tiada TimeOut</th>");
            sb.AppendLine("<th style='padding: 8px; text-align: center; min-width: 80px;'>Ponteng</th>");
            sb.AppendLine("<th style='padding: 8px; text-align: center; min-width: 100px;'>Ponteng Separuh Hari (TimeIn)</th>");
            sb.AppendLine("<th style='padding: 8px; text-align: center; min-width: 100px;'>Ponteng Separuh Hari (TimeOut)</th>");
            sb.AppendLine("</tr>");

            // Data rows
            int bil = 1;
            foreach (var staff in staffSummary)
            {
                sb.AppendLine("<tr style='border-bottom: 1px solid #ccc;'>");
                sb.AppendLine($"<td align='center' style='padding: 5px;'>{bil}</td>");
                sb.AppendLine($"<td align='center' style='padding: 5px;'>{staff.IdStaff ?? ""}</td>");
                sb.AppendLine($"<td style='padding: 5px;'>{staff.Name ?? ""}</td>");
                sb.AppendLine($"<td style='padding: 5px;'>{staff.Jawatan ?? ""}</td>");
                sb.AppendLine($"<td style='padding: 5px;'>{staff.Jabatan ?? ""}</td>");
                sb.AppendLine($"<td style='padding: 5px;'>{staff.Bahagian ?? " - "}</td>");
                sb.AppendLine($"<td align='center' style='padding: 5px;'>{staff.TiadaKenyataan}</td>");
                sb.AppendLine($"<td align='center' style='padding: 5px;'>{staff.DatangLewat}</td>");
                sb.AppendLine($"<td align='center' style='padding: 5px;'>{staff.BalikCepat}</td>");
                sb.AppendLine($"<td align='center' style='padding: 5px;'>{staff.TidakPunchIn}</td>");
                sb.AppendLine($"<td align='center' style='padding: 5px;'>{staff.TidakPunchOut}</td>");
                sb.AppendLine($"<td align='center' style='padding: 5px;'>{staff.Ponteng}</td>");
                sb.AppendLine($"<td align='center' style='padding: 5px;'>{staff.PontengSeparuhHariTimeIn}</td>");
                sb.AppendLine($"<td align='center' style='padding: 5px;'>{staff.PontengSeparuhHariTimeOut}</td>");
                sb.AppendLine($"<td align='center' style='padding: 5px; background-color: #ffeeee; font-weight: bold;'>{staff.TotalKesalahan}</td>");
                sb.AppendLine("</tr>");
                bil++;
            }

            // Summary row
            if (staffSummary.Any())
            {
                sb.AppendLine("<tr style='background-color: #f0f0f0; font-weight: bold; border-top: 2px solid #000;'>");
                sb.AppendLine("<td colspan='6' align='center' style='padding: 8px; font-weight: bold;'>JUMLAH KESELURUHAN</td>");
                sb.AppendLine($"<td align='center' style='padding: 8px;'>{staffSummary.Sum(x => x.TiadaKenyataan)}</td>");
                sb.AppendLine($"<td align='center' style='padding: 8px;'>{staffSummary.Sum(x => x.DatangLewat)}</td>");
                sb.AppendLine($"<td align='center' style='padding: 8px;'>{staffSummary.Sum(x => x.BalikCepat)}</td>");
                sb.AppendLine($"<td align='center' style='padding: 8px;'>{staffSummary.Sum(x => x.TidakPunchIn)}</td>");
                sb.AppendLine($"<td align='center' style='padding: 8px;'>{staffSummary.Sum(x => x.TidakPunchOut)}</td>");
                sb.AppendLine($"<td align='center' style='padding: 8px;'>{staffSummary.Sum(x => x.Ponteng)}</td>");
                sb.AppendLine($"<td align='center' style='padding: 8px;'>{staffSummary.Sum(x => x.PontengSeparuhHariTimeIn)}</td>");
                sb.AppendLine($"<td align='center' style='padding: 8px;'>{staffSummary.Sum(x => x.PontengSeparuhHariTimeOut)}</td>");
                sb.AppendLine($"<td align='center' style='padding: 8px; background-color: #ffcccc; font-weight: bold;'>{staffSummary.Sum(x => x.TotalKesalahan)}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</table>");

            // Generate filename with profile info if applicable
            string reportTypePrefix = hasSearchFilters ? "Summary Report" : "All Records Summary";
            string fileName = SelectedRecordId.HasValue
                ? $"{reportTypePrefix} - Profile {SelectedRecordId.Value}.xls"
                : $"{reportTypePrefix}.xls";

            // Return as file download
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "application/vnd.ms-excel", fileName);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error generating Excel export: {ex.Message}";
            return RedirectToAction("Search");
        }
    }

    [HttpPost]
    public async Task<IActionResult> ExportDetailedToExcelHtml(
        string searchIdStaff = "", string searchNama = "",
        string searchSyrt = "", string searchJab = "", string searchBhgn = "", string searchJaw = "",
        string dateFrom = "", string dateTo = "", string searchSelectedNama = "", int? SelectedRecordId = null,
        List<string> SelectedUserIds = null)
    {
        try
        {
            // Parse dates
            DateTime? parsedDateFrom = null;
            DateTime? parsedDateTo = null;

            if (DateTime.TryParse(dateFrom, out DateTime tempDateFrom))
                parsedDateFrom = tempDateFrom;

            if (DateTime.TryParse(dateTo, out DateTime tempDateTo))
                parsedDateTo = tempDateTo;

            // Check if any search filters are applied
            bool hasSearchFilters = !string.IsNullOrEmpty(searchIdStaff) ||
                                   !string.IsNullOrEmpty(searchNama) ||
                                   !string.IsNullOrEmpty(searchSyrt) ||
                                   !string.IsNullOrEmpty(searchJab) ||
                                   !string.IsNullOrEmpty(searchBhgn) ||
                                   !string.IsNullOrEmpty(searchJaw) ||
                                   !string.IsNullOrEmpty(searchSelectedNama) ||
                                   (SelectedUserIds != null && SelectedUserIds.Any()) ||
                                   parsedDateFrom.HasValue ||
                                   parsedDateTo.HasValue;

            // Build base query
            var query = _context.tbl_ATTKesalahan.AsQueryable();

            // If SelectedRecordId is provided, filter by it
            if (SelectedRecordId.HasValue)
                query = query.Where(x => x.IdProfile == SelectedRecordId.Value);

            // Apply search filters only if they exist
            if (hasSearchFilters)
            {
                if (!string.IsNullOrEmpty(searchIdStaff))
                    query = query.Where(x => x.IdStaff != null && x.IdStaff.Contains(searchIdStaff));

                if (!string.IsNullOrEmpty(searchNama))
                    query = query.Where(x => x.Name != null && x.Name.Contains(searchNama));

                if (SelectedUserIds != null && SelectedUserIds.Any())
                    query = query.Where(x => SelectedUserIds.Contains(x.IdStaff));
                else if (!string.IsNullOrEmpty(searchSelectedNama))
                    query = query.Where(x => x.IdStaff == searchSelectedNama);

                if (!string.IsNullOrEmpty(searchSyrt))
                    query = query.Where(x => x.Syrt != null && x.Syrt.Contains(searchSyrt));

                if (!string.IsNullOrEmpty(searchJab))
                    query = query.Where(x => x.Jab != null && x.Jab.Contains(searchJab));

                if (!string.IsNullOrEmpty(searchBhgn))
                    query = query.Where(x => x.Bhgn != null && x.Bhgn.Contains(searchBhgn));

                if (!string.IsNullOrEmpty(searchJaw))
                    query = query.Where(x => x.Jaw != null && x.Jaw.Contains(searchJaw));

                if (parsedDateFrom.HasValue)
                    query = query.Where(x => x.Trk.Date >= parsedDateFrom.Value.Date);

                if (parsedDateTo.HasValue)
                    query = query.Where(x => x.Trk.Date <= parsedDateTo.Value.Date);
            }

            // Exclude non-kesalahan
            query = query.Where(x => x.JenisKesalahan != "Hari Tidak Bekerja");

            // Apply filter condition based on whether search filters are applied
            var kesalahanRecords = hasSearchFilters ?
                await query.Where(x => (x.Exclude == false || x.Exclude == null) && x.IdSemakan == null).ToListAsync() :
                await query.ToListAsync();

            // Format dates and determine report type
            string reportType = hasSearchFilters ? "FILTERED" : "ALL EXISTING RECORDS";
            string titleSuffix = SelectedRecordId.HasValue ? $" - Profile ID: {SelectedRecordId.Value}" : "";

            // Group by staff/date/jenis & calculate BilHari based on JenisKesalahan
            var detailedSummary = kesalahanRecords
                .GroupBy(x => new { x.IdStaff, x.Name, Date = x.Trk.Date, x.JenisKesalahan })
                .Select(g =>
                {
                    var jenisKesalahan = g.Key.JenisKesalahan;

                    // Set BilHari based on JenisKesalahan
                    double bilHari = jenisKesalahan switch
                    {
                        "Ponteng" => 1.0,
                        "Ponteng Separuh Hari (TimeIn)" => 0.5,
                        "Ponteng Separuh Hari (TimeOut)" => 0.5,
                        "Tiada TimeIn" => 0.5,
                        "Tiada TimeOut" => 0.5,
                        _ => 0.0 // For "Datang Lewat", "Balik Awal", and others
                    };

                    return new
                    {
                        IdStaff = g.Key.IdStaff,
                        Name = g.Key.Name,
                        Tarikh = g.Key.Date,
                        JenisKesalahan = jenisKesalahan,
                        BilKesalahan = g.Count(),
                        BilHari = bilHari
                    };
                })
                .OrderBy(x => x.IdStaff)
                .ThenBy(x => x.Tarikh)
                .ThenBy(x => x.JenisKesalahan)
                .ToList();

            var sb = new StringBuilder();

            // CSS
            sb.AppendLine("<style>");
            sb.AppendLine("table { border-collapse: collapse; font-family: Arial, sans-serif; width: 100%; }");
            sb.AppendLine("th, td { border: 1px solid black; padding: 8px; text-align: left; }");
            sb.AppendLine("th {font-weight: bold; }");
            sb.AppendLine("</style>");

            // Header
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><td colspan='7' align='center' style='font-weight: bold; padding: 10px; background-color: #e6f3ff;'>");
            sb.AppendLine($"<b>REKOD TERPERINCI KESALAHAN KEHADIRAN ({reportType}){titleSuffix}<br></b>");
            sb.AppendLine("</td></tr>");

            // Add note about report type
            if (!hasSearchFilters)
            {
                sb.AppendLine("<tr><td colspan='7' align='center' style='padding: 5px; background-color: #fff3cd; font-style: italic;'>");
                sb.AppendLine("Note: This report shows all existing records without date or other filters");
                sb.AppendLine("</td></tr>");
            }

            sb.AppendLine("<tr>");
            sb.AppendLine("<th>Bil</th>");
            sb.AppendLine("<th>No Pekerja</th>");
            sb.AppendLine("<th>Nama</th>");
            sb.AppendLine("<th>Tarikh</th>");
            sb.AppendLine("<th>Jenis Kesalahan</th>");
            sb.AppendLine("<th>Bilangan Kesalahan</th>");
            sb.AppendLine("<th>Bilangan Hari</th>");
            sb.AppendLine("</tr>");

            // Detail rows
            int bil = 1;
            foreach (var record in detailedSummary)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td align='center'>{bil}</td>");
                sb.AppendLine($"<td>{record.IdStaff}</td>");
                sb.AppendLine($"<td>{record.Name}</td>");
                sb.AppendLine($"<td align='center'>{record.Tarikh:dd/MM/yyyy}</td>");
                sb.AppendLine($"<td>{record.JenisKesalahan}</td>");
                sb.AppendLine($"<td align='center'>{record.BilKesalahan}</td>");
                sb.AppendLine($"<td align='center'>{record.BilHari}</td>");
                sb.AppendLine("</tr>");
                bil++;
            }

            sb.AppendLine("</table><br><br>");

            // Summary
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><td colspan='5' align='center' style='font-weight: bold; padding: 10px;'>");
            sb.AppendLine($"<b>RINGKASAN KESELURUHAN KESALAHAN ({reportType}){titleSuffix}<br></b>");
            sb.AppendLine("</td></tr>");

            sb.AppendLine("<tr>");
            sb.AppendLine("<th>Bil</th>");
            sb.AppendLine("<th>No Pekerja</th>");
            sb.AppendLine("<th>Nama</th>");
            sb.AppendLine("<th>Jumlah Kesalahan</th>");
            sb.AppendLine("<th>Bilangan Hari</th>");
            sb.AppendLine("</tr>");

            int summaryBil = 1;
            foreach (var staffGroup in detailedSummary.GroupBy(x => new { x.IdStaff, x.Name }))
            {
                int jumlahKesalahan = staffGroup.Sum(x => x.BilKesalahan);
                double jumlahHari = staffGroup.Sum(x => x.BilHari);

                sb.AppendLine("<tr>");
                sb.AppendLine($"<td align='center'>{summaryBil}</td>");
                sb.AppendLine($"<td align='center'>{staffGroup.Key.IdStaff}</td>");
                sb.AppendLine($"<td>{staffGroup.Key.Name}</td>");
                sb.AppendLine($"<td align='center'>{jumlahKesalahan}</td>");
                sb.AppendLine($"<td align='center'>{jumlahHari}</td>");
                sb.AppendLine("</tr>");

                summaryBil++;
            }

            sb.AppendLine("<tr style='background-color: #f9f9f9;'>");
            sb.AppendLine("<td colspan='3' align='center'><b>JUMLAH KESELURUHAN</b></td>");
            sb.AppendLine($"<td align='center'><b>{detailedSummary.Sum(x => x.BilKesalahan)}</b></td>");
            sb.AppendLine($"<td align='center'><b>{detailedSummary.Sum(x => x.BilHari)}</b></td>");
            sb.AppendLine("</tr>");

            sb.AppendLine("</table>");

            // Generate filename with profile info if applicable
            string reportTypePrefix = hasSearchFilters ? "Detail Report" : "All Records Detail";
            string fileName = SelectedRecordId.HasValue
                ? $"{reportTypePrefix} - Profile {SelectedRecordId.Value}.xls"
                : $"{reportTypePrefix}.xls";

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "application/vnd.ms-excel", fileName);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error generating detailed Excel export: {ex.Message}";
            return RedirectToAction("Search");
        }
    }
}