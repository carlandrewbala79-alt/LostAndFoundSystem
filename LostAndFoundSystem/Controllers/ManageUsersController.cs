using LostAndFoundSystem.Data;
using LostAndFoundSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LostAndFoundSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ManageUsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ManageUsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /ManageUsers
        [HttpGet]
        public async Task<IActionResult> Index(string filter = "All Users")
        {
            var query = _context.Users.AsNoTracking().AsQueryable();

            switch (filter)
            {
                case "Students":
                    query = query.Where(u => u.Role == "Student");
                    break;
                case "Administrators":
                    query = query.Where(u => u.Role == "Admin");
                    break;
                default:
                    filter = "All Users";
                    break;
            }

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            ViewBag.Filter = filter;
            ViewBag.TotalUsers = users.Count;

            return View(users);
        }

        // POST: /ManageUsers/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var currentUserIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(currentUserIdClaim, out int currentUserId) && currentUserId == id)
            {
                TempData["ErrorMessage"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == id);
            if (user == null)
                return NotFound();

            // Prevent deleting the last admin
            if (user.Role == "Admin")
            {
                var adminCount = await _context.Users.CountAsync(u => u.Role == "Admin");
                if (adminCount <= 1)
                {
                    TempData["ErrorMessage"] = "Cannot delete the last administrator account.";
                    return RedirectToAction(nameof(Index));
                }
            }

            // Remove related claims and items
            var claims = _context.Claims.Where(c => c.ClaimedByUserID == id);
            _context.Claims.RemoveRange(claims);

            var items = _context.Items.Where(i => i.ReportedByUserID == id);
            var itemIds = items.Select(i => i.ItemID).ToList();
            var itemClaims = _context.Claims.Where(c => itemIds.Contains(c.ItemID));
            _context.Claims.RemoveRange(itemClaims);
            _context.Items.RemoveRange(items);

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"User \"{user.FullName}\" has been deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
