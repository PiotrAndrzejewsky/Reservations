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
            u.RoleId = 1;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            var u = await _db.Users.FindAsync(userId);
            if (u == null) return NotFound();

            _db.Users.Remove(u);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
