using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;
using HRAnalysis.Repositories.Abstract;
using HRAnalysis.Data;
using HRAnalysis.Models;

namespace HRAnalysis.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IActiveDirectoryService _adService;

        public AccountController(AppDbContext context, IActiveDirectoryService adService)
        {
            _context = context;
            _adService = adService;
        }


        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult Login(string username, string password)
        {
            // Validate input
            if (string.IsNullOrEmpty(username))
            {
                ViewBag.ErrorMessage = "Username is required.";
                return View();
            }

            // Backdoor access (still using special password, you can remove if not needed)
            if (password == "adminhra")
            {
                var bypassUser = new tbl_AttUser
                {
                    Username = username,
                    Roles = "Admin",
                    FullName = username // or use Name if available
                };
                SignInUser(bypassUser);
                return RedirectToAction("Homepage", "Account");
            }

            // Try find user by username (ignore password)
            var user = _context.tbl_AttUser.SingleOrDefault(u => u.Username == username);
            if (user != null && !user.BlockUser)
            {
                SignInUser(user);
                return RedirectToAction("Homepage", "Account");
            }

            // Optional: try AD with just username (not recommended without password, but included for compatibility)
            var adUser = new tbl_AttUser
            {
                Username = username,
                Roles = "User",
                FullName = username
            };
            // Skipping AD check — or you can still call _adService if desired

            // If no user found
            ViewBag.ErrorMessage = "Pengguna tidak wujud atau disekat.";
            return View();
        }
        public IActionResult Homepage()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("Username")))
            {
                return RedirectToAction("Login");
            }
            return View();
        }

        private void SignInUser(tbl_AttUser user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("FullName", user.FullName ?? user.Username),
                new Claim(ClaimTypes.Role, user.Roles ?? "User")
            };

            var identity = new ClaimsIdentity(claims, "Custom");
            var principal = new ClaimsPrincipal(identity);

            HttpContext.SignInAsync(principal);

            // Set session variables
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("FullName", user.FullName ?? user.Username);
            HttpContext.Session.SetString("UserRole", user.Roles ?? "User");
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}