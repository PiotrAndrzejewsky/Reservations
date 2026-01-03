using Microsoft.AspNetCore.Mvc;
using Reservations.Data;
using Reservations.Models;
using System.Security.Cryptography;
using System.Text;

namespace Reservations.Controllers
{
    public class AccountController : Controller
    {
        private readonly ReservationsDbContext _db;

        public AccountController(ReservationsDbContext db)
        {
            _db = db;
        }

        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string username, string password, string returnUrl = null)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Nazwa u¿ytkownika i has³o s¹ wymagane");
                return View();
            }

            var hash = HashPassword(password);
            var user = _db.Users.FirstOrDefault(u => u.UserName == username && u.PasswordHash == hash);
            if (user == null)
            {
                ModelState.AddModelError("", "Nieprawid³owe dane logowania");
                return View();
            }

            // Set a simple session-based authentication
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.UserName);
            HttpContext.Session.SetInt32("RoleId", user.RoleId);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(string username, string email, string password, int role = 3)
        {
            // Only allow Trainer (2) or User (3) roles from the UI. Never allow assigning Administrator (1) via client.
            if (role != 2)
            {
                role = 3; // default to User
            }

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Nazwa u¿ytkownika i has³o s¹ wymagane");
                return View();
            }

            if (_db.Users.Any(u => u.UserName == username))
            {
                ModelState.AddModelError("", "Nazwa u¿ytkownika ju¿ istnieje");
                return View();
            }

            var user = new User
            {
                UserName = username,
                Email = email ?? string.Empty,
                RoleId = role, // role chosen by user (2=Trainer,3=User)
                PasswordHash = HashPassword(password)
            };

            _db.Users.Add(user);
            _db.SaveChanges();

            // Auto-login
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.UserName);
            HttpContext.Session.SetInt32("RoleId", user.RoleId);

            return RedirectToAction("Index", "Home");
        }
    }
}
