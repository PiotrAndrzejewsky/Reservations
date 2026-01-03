using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reservations.Data;
using Reservations.Models;
using Reservations.Filters;

namespace Reservations.Controllers
{
    [RoleAuthorize("Administrator")]
    public class AdminController : Controller
    {
        private readonly ReservationsDbContext _db;

        public AdminController(ReservationsDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _db.Users.Include(u => u.Role).ToListAsync();
            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PromoteToAdmin(int userId)
        {
            var u = await _db.Users.FindAsync(userId);
            if (u == null) return NotFound();

            u.RoleId = 1; // Administrator
            await _db.SaveChangesAsync();
            TempData["Info"] = "U¿ytkownik zosta³ uaktualniony do roli Administratora.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            var u = await _db.Users.FindAsync(userId);
            if (u == null) return NotFound();

            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId != null && currentUserId.Value == u.Id)
            {
                TempData["Error"] = "Nie mo¿na usun¹æ w³asnego konta.";
                return RedirectToAction(nameof(Index));
            }

            // Ensure we don't remove the last administrator
            var adminsCount = await _db.Users.CountAsync(x => x.RoleId == 1);
            if (u.RoleId == 1 && adminsCount <= 1)
            {
                TempData["Error"] = "Nie mo¿na usun¹æ ostatniego administratora.";
                return RedirectToAction(nameof(Index));
            }

            _db.Users.Remove(u);
            await _db.SaveChangesAsync();

            TempData["Info"] = "U¿ytkownik zosta³ usuniêty.";
            return RedirectToAction(nameof(Index));
        }
    }
}
