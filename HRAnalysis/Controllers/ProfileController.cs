using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HRAnalysis.Data;
using HRAnalysis.Models;

namespace HRAnalysis.Controllers
{
    public class ProfileController : Controller
    {
        private readonly AppDbContext _context;

        public ProfileController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Profile
        public async Task<IActionResult> Index()
        {
            var loggedInUsername = HttpContext.Session.GetString("Username");

            if (_context.tbl_Profile == null)
            {
                return Problem("Entity set 'AppDbContext.tbl_Profile' is null.");
            }

            var filteredProfiles = await _context.tbl_Profile
                .Where(p => p.UpdatedBy == loggedInUsername)
                .ToListAsync();

            return View(filteredProfiles);
        }



        // GET: Profile/Create
        public IActionResult Create()
        {
            var loggedInUsername = HttpContext.Session.GetString("Username");
            ViewBag.LoggedInUsername = loggedInUsername;
            return View();
        }


        // POST: Profile/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,UpdatedBy")] tbl_Profile tbl_Profile)
        {
            if (ModelState.IsValid)
            {
                // Set creation datetime
                tbl_Profile.CreatedAt = DateTime.Now;

                _context.Add(tbl_Profile);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Profile berjaya ditambah";
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = "Terdapat ralat dalam data yang dimasukkan";
            return RedirectToAction(nameof(Index));
        }


        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.tbl_Profile == null)
            {
                return NotFound();
            }

            var tbl_Profile = await _context.tbl_Profile
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tbl_Profile == null)
            {
                return NotFound();
            }

            return View(tbl_Profile);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (_context.tbl_Profile == null)
            {
                TempData["ErrorMessage"] = "Entity set 'AppDbContext.tbl_Profile' is null.";
                return RedirectToAction(nameof(Index));
            }

            var tbl_Profile = await _context.tbl_Profile.FindAsync(id);
            if (tbl_Profile != null)
            {
                // Remove all related tbl_ATTKesalahan records
                var relatedKesalahan = _context.tbl_ATTKesalahan
                    .Where(k => k.IdProfile == id);
                _context.tbl_ATTKesalahan.RemoveRange(relatedKesalahan);

                // Remove all related tbl_ATTSemakanV1 records
                var relatedSemakan = _context.tbl_ATTSemakanV1
                    .Where(s => s.IdProfile == id);
                _context.tbl_ATTSemakanV1.RemoveRange(relatedSemakan);

                // Then remove the profile
                _context.tbl_Profile.Remove(tbl_Profile);

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Profile dan semua rekod berkaitan berjaya dipadam.";
            }
            else
            {
                TempData["ErrorMessage"] = "Profile tidak dijumpai.";
            }

            return RedirectToAction(nameof(Index));
        }



        private bool tbl_ProfileExists(int id)
        {
          return (_context.tbl_Profile?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
