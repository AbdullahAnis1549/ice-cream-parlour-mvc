using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using WebApplication12.Models;
using WebApplication12.Data;
using Microsoft.EntityFrameworkCore;
using WebApplication12.ViewModels;

namespace WebApplication12.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = new HomeViewModel
            {
                Winners = await _context.UserSubmissions
                    .Include(s => s.User)
                    .Where(s => s.EnlistedOnHome)
                    .ToListAsync(),
                Deals = await _context.Deals.ToListAsync(),
                Slides = await _context.HeroSliders.OrderBy(s => s.DisplayOrder).ToListAsync()
            };
            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Feedback(Feedback model)
        {
            if (ModelState.IsValid)
            {
                _context.Feedbacks.Add(model);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Your testimonial has been archived. Thank you.";
                return RedirectToAction("Contact");
            }
            return View(model);
        }

        public IActionResult FAQ()
        {
            return View();
        }
    }
}
