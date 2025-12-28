using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Reservations.Data;
using Reservations.Models;

namespace Reservations.Pages.Sessions
{
    public class IndexModel : PageModel
    {
        private readonly ReservationsDbContext _db;

        public IndexModel(ReservationsDbContext db)
        {
            _db = db;
        }

        public IList<Session> Sessions { get; set; } = new List<Session>();

        [BindProperty]
        public InputModel NewSession { get; set; } = new InputModel();

        public class InputModel
        {
            public string Title { get; set; } = string.Empty;
            public DateTime Start { get; set; } = DateTime.UtcNow;
            public DateTime End { get; set; } = DateTime.UtcNow.AddHours(1);
            public int AvailableSlots { get; set; } = 10;
        }

        public async Task OnGetAsync()
        {
            Sessions = await _db.Sessions.OrderBy(s => s.Start).ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await OnGetAsync();
                return Page();
            }

            var session = new Session
            {
                Title = NewSession.Title,
                Start = NewSession.Start,
                End = NewSession.End,
                AvailableSlots = NewSession.AvailableSlots
            };

            _db.Sessions.Add(session);
            await _db.SaveChangesAsync();

            return RedirectToPage();
        }
    }
}
