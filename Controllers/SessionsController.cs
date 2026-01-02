using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reservations.Data;
using Reservations.Models;
using Reservations.Filters;

namespace Reservations.Controllers
{
    public class SessionsController : Controller
    {
        private readonly ReservationsDbContext _db;
        private const int SlotMinutes = 30;

        public SessionsController(ReservationsDbContext db)
        {
            _db = db;
        }

        private static DateTime AlignToSlotUtc(DateTime dt)
        {
            var utc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
            var minutes = utc.Minute;
            var alignedMinutes = minutes < SlotMinutes ? 0 : SlotMinutes;
            return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, alignedMinutes, 0, DateTimeKind.Utc);
        }

        public class SessionInputModel
        {
            private static DateTime DefaultStartUtc => AlignToSlotUtc(DateTime.UtcNow);
            public string Title { get; set; } = string.Empty;
            public DateTime Start { get; set; } = DefaultStartUtc;
            public DateTime End { get; set; } = DefaultStartUtc.AddMinutes(SlotMinutes);
            public int AvailableSlots { get; set; } = 10;
        }

        public class IndexViewModel
        {
            public IEnumerable<Session> Sessions { get; set; } = Enumerable.Empty<Session>();
            public SessionInputModel NewSession { get; set; } = new SessionInputModel();
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var vm = new IndexViewModel
            {
                Sessions = await _db.Sessions.OrderBy(s => s.Start).ToListAsync()
            };

            return View(vm);
        }

        private bool IsStepValid(DateTime dt) => dt.Minute % SlotMinutes == 0 && dt.Second == 0 && dt.Millisecond == 0;

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize("Trainer", "Administrator")]
        public async Task<IActionResult> Create(SessionInputModel input)
        {
            if (!ModelState.IsValid)
            {
                var vmInvalid = new IndexViewModel { Sessions = await _db.Sessions.OrderBy(s => s.Start).ToListAsync(), NewSession = input };
                return View("Index", vmInvalid);
            }

            if (!IsStepValid(input.Start) || !IsStepValid(input.End) || input.End <= input.Start)
            {
                TempData["Error"] = "Czas musi byæ w krokach co 30 minut, bez sekund, a koniec po pocz¹tku.";
                var vmError = new IndexViewModel { Sessions = await _db.Sessions.OrderBy(s => s.Start).ToListAsync(), NewSession = input };
                return View("Index", vmError);
            }

            DateTime startLocal = DateTime.SpecifyKind(input.Start, DateTimeKind.Local);
            DateTime endLocal = DateTime.SpecifyKind(input.End, DateTimeKind.Local);
            DateTime startUtc = startLocal.ToUniversalTime();
            DateTime endUtc = endLocal.ToUniversalTime();

            var session = new Session
            {
                Title = input.Title,
                Start = startUtc,
                End = endUtc,
                AvailableSlots = input.AvailableSlots
            };

            _db.Sessions.Add(session);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reserve(int sessionId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var roleId = HttpContext.Session.GetInt32("RoleId");
            if (userId == null) return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Sessions") });

            // Trainers are not allowed to reserve
            if (roleId == 2)
            {
                TempData["Error"] = "Trainers cannot reserve sessions.";
                return RedirectToAction(nameof(Index));
            }

            var session = await _db.Sessions.FindAsync(sessionId);
            if (session == null) return NotFound();

            // Prevent double reservation by same user
            var existing = await _db.Reservations.FirstOrDefaultAsync(r => r.SessionId == sessionId && r.UserId == userId.Value);
            if (existing != null)
            {
                TempData["Info"] = "You already have a reservation for this session.";
                return RedirectToAction(nameof(Index));
            }

            // Check available slots
            var reservedCount = await _db.Reservations.CountAsync(r => r.SessionId == sessionId);
            if (reservedCount >= session.AvailableSlots)
            {
                TempData["Error"] = "No available slots";
                return RedirectToAction(nameof(Index));
            }

            var reservation = new Reservation
            {
                UserId = userId.Value,
                SessionId = sessionId
            };

            _db.Reservations.Add(reservation);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unreserve(int sessionId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Sessions") });

            var existing = await _db.Reservations.FirstOrDefaultAsync(r => r.SessionId == sessionId && r.UserId == userId.Value);
            if (existing == null)
            {
                TempData["Info"] = "You don't have a reservation for this session.";
                return RedirectToAction(nameof(Index));
            }

            _db.Reservations.Remove(existing);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize("Administrator")]
        public async Task<IActionResult> DeleteSession(int sessionId)
        {
            var s = await _db.Sessions.FindAsync(sessionId);
            if (s == null) return NotFound();

            // remove reservations first
            var res = _db.Reservations.Where(r => r.SessionId == sessionId);
            _db.Reservations.RemoveRange(res);
            _db.Sessions.Remove(s);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
