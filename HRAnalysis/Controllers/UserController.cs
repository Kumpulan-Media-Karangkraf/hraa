using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using HRAnalysis.Data;
using HRAnalysis.Models;

namespace HRAnalysis.Controllers
{
    public class UserController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UserController> _logger;
        private object _userManager;

        public UserController(AppDbContext context, ILogger<UserController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _context.tbl_AttUser.ToListAsync();
            return View(users); // Ensure you're passing the correct data here
        }

        // GET: Users/Create
        public IActionResult Create()
        {
            var loggedInUsername = HttpContext.Session.GetString("Username");
            ViewBag.LoggedInUsername = loggedInUsername;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FullName,Username,Password,Active,Roles,UseWindowsAuth,IdUser,IdStaff,BlockUser")] tbl_AttUser user)
        {
            //var loggedInUsername = HttpContext.Session.GetString("Username");

            if (user.UseWindowsAuth)
            {
                ModelState.Remove("Password");
                user.Password = null;
            }
            else
            {
                if (string.IsNullOrEmpty(user.Password))
                {
                    ModelState.AddModelError("Password", "The Password field is required.");
                }
            }


            if (ModelState.IsValid)
            {
                try
                {
                  
                    user.LastUpdate = DateTime.Now;
                    _context.Add(user);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException is SqlException sqlException &&
                        (sqlException.Number == 2601 || sqlException.Number == 2627))
                    {
                        if (ex.Message.Contains("IX_users_StaffNum")) // Check if it's for StaffNum
                        {
                            ModelState.AddModelError("StaffNum", "Staff Number already exists.");
                        }
                        else if (ex.Message.Contains("IX_users_Username")) // Check if it's for Username
                        {
                            ModelState.AddModelError("Username", "Username already exists.");
                        }
                        else
                        {
                            ModelState.AddModelError(string.Empty, "A unique constraint error occurred. Please ensure all fields are unique.");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "An error occurred while saving the user. Please try again.");
                    }
                }
            }

            return View(user);
        }


        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            var loggedInUsername = HttpContext.Session.GetString("Username");
            ViewBag.LoggedInUsername = loggedInUsername;

            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.tbl_AttUser.FirstOrDefaultAsync(m => m.IdUser == id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, tbl_AttUser user)
        {
            var loggedInUsername = HttpContext.Session.GetString("Username");
            ViewBag.LoggedInUsername = loggedInUsername;

            if (id != user.IdUser)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingUser = await _context.tbl_AttUser.AsNoTracking().FirstOrDefaultAsync(u => u.IdUser == id);
                    if (string.IsNullOrEmpty(user.Password))
                    {
                        user.Password = existingUser.Password;
                    }

                    if (user.UseWindowsAuth)
                    {
                        user.Password = null;
                    }

                    user.LastUpdate = DateTime.Now;

                    _context.Update(user);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(user.IdUser))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(user);
        }


        [HttpPost]
        [Route("User/Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                // Check if the provided ID is valid
                if (id <= 0)
                {
                    return Json(new { success = false, message = "Invalid user ID provided." });
                }

                // Retrieve the user from the database
                var user = await _context.tbl_AttUser
                    .FirstOrDefaultAsync(u => u.IdUser == id);

                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                // Remove the user and save changes
                _context.tbl_AttUser.Remove(user);
                await _context.SaveChangesAsync();

                // Log the successful deletion
                _logger.LogInformation($"User with ID {id} was successfully deleted");

                return Json(new { success = true, message = "User deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred while deleting user with ID {id}");
                return Json(new { success = false, message = "An error occurred while deleting the user." });
            }
        }

        private bool UserExists(int id)
        {
            return _context.tbl_AttUser.Any(e => e.IdUser == id);
        }
    }
}