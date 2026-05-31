using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication12.Data;
using WebApplication12.Models;

namespace WebApplication12.Controllers
{
    public class RecipesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RecipesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // List all ice cream flavors
        public async Task<IActionResult> Index(string? type)
        {
            var query = _context.Recipes.AsQueryable();
            if (type == "free")
            {
                query = query.Where(r => r.IsFree);
                ViewBag.Filter = "Free Recipes";
            }
            var recipes = await query.ToListAsync();
            return View(recipes);
        }

        // View specific recipe details
        public async Task<IActionResult> Details(int id)
        {
            var recipe = await _context.Recipes.FindAsync(id);
            if (recipe == null) return NotFound();

            // Check if user is logged in and is a paid member
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                TempData["ErrorMessage"] = "Please register and pay to view this delicious recipe!";
                return RedirectToAction("Register", "Account");
            }

            var userId = int.Parse(userIdStr);
            var user = await _context.Users.FindAsync(userId);

            if (user == null || (!user.IsPaidMember && !recipe.IsFree))
            {
                TempData["ErrorMessage"] = "This recipe is exclusive to our Monthly/Yearly members. Upgrade now!";
                return RedirectToAction("Profile", "Account");
            }

            return View(recipe);
        }

        // Page for users to submit their own recipes
        [HttpGet]
        public IActionResult Submit()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserId")))
            {
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(UserSubmission model)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.UserId = int.Parse(userIdStr);
            model.Status = "Pending";
            
            _context.UserSubmissions.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Your recipe has been submitted for review. If selected, you will win prize money and a certificate!";
            return RedirectToAction("Index");
        }
    }
}
