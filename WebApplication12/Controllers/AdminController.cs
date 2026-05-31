using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication12.Data;
using WebApplication12.Models;
using WebApplication12.Services;

namespace WebApplication12.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly CloudinaryService _cloudinaryService;

        public AdminController(ApplicationDbContext context, IWebHostEnvironment env, CloudinaryService cloudinaryService)
        {
            _context = context;
            _env = env;
            _cloudinaryService = cloudinaryService;
        }

        // Middleware-like check for Admin role
        private bool IsAdmin() => HttpContext.Session.GetString("UserRole") == "admin";

        public async Task<IActionResult> Dashboard()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var userCount = await _context.Users.CountAsync();
            var recipeCount = await _context.Recipes.CountAsync();
            var orderCount = await _context.BookOrders.CountAsync();
            var feedbackCount = await _context.Feedbacks.CountAsync();
            var dealCount = await _context.Deals.CountAsync();

            ViewBag.UserCount = userCount;
            ViewBag.RecipeCount = recipeCount;
            ViewBag.OrderCount = orderCount;
            ViewBag.FeedbackCount = feedbackCount;
            ViewBag.DealCount = dealCount;

            var orders = await _context.BookOrders.ToListAsync();
            
            decimal totalRevenue = orders.Sum(o => o.TotalCost);
            decimal avgOrderValue = orderCount > 0 ? totalRevenue / orderCount : 0;
            
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.AvgOrderValue = avgOrderValue;

            // Recent Orders
            ViewBag.RecentOrders = orders.OrderByDescending(o => o.OrderDate).Take(3).ToList();

            // Order Status Overview
            ViewBag.OrderedCount = orders.Count(o => o.Status == "Ordered");
            ViewBag.CompletedCount = orders.Count(o => o.Status == "Completed");
            ViewBag.ProcessedCount = orders.Count(o => o.Status == "Processed");
            ViewBag.CancelledCount = orders.Count(o => o.Status == "Cancelled");

            // Top Customers
            ViewBag.TopCustomers = orders.GroupBy(o => o.CustomerName)
                .Select(g => new { 
                    CustomerName = g.Key, 
                    Orders = g.Count(), 
                    TotalSpent = g.Sum(o => o.TotalCost), 
                    LastOrder = g.Max(o => o.OrderDate) 
                })
                .OrderByDescending(c => c.TotalSpent)
                .Take(3)
                .ToList();

            return View();
        }

        #region Recipe Management
        public async Task<IActionResult> Recipes()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            return View(await _context.Recipes.ToListAsync());
        }

        [HttpGet]
        public IActionResult CreateRecipe() => IsAdmin() ? View() : RedirectToAction("Login", "Account");

        [HttpPost]
        public async Task<IActionResult> CreateRecipe(Recipe recipe)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            
            if (recipe.ImageFile != null)
            {
                recipe.ImageUrl = await SaveFile(recipe.ImageFile, "recipes");
            }

            _context.Recipes.Add(recipe);
            await _context.SaveChangesAsync();
            return RedirectToAction("Recipes");
        }

        [HttpGet]
        public async Task<IActionResult> EditRecipe(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            var recipe = await _context.Recipes.FindAsync(id);
            if (recipe == null) return NotFound();
            return View(recipe);
        }

        [HttpPost]
        public async Task<IActionResult> EditRecipe(Recipe recipe)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            
            var existing = await _context.Recipes.FindAsync(recipe.Id);
            if (existing == null) return NotFound();

            existing.Name = recipe.Name;
            existing.Description = recipe.Description;
            existing.Ingredients = recipe.Ingredients;
            existing.Procedure = recipe.Procedure;
            existing.IsFree = recipe.IsFree;

            if (recipe.ImageFile != null)
            {
                existing.ImageUrl = await SaveFile(recipe.ImageFile, "recipes");
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Recipes");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteRecipe(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            var recipe = await _context.Recipes.FindAsync(id);
            if (recipe != null)
            {
                _context.Recipes.Remove(recipe);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Recipes");
        }
        #endregion

        #region Order Management
        public async Task<IActionResult> Orders()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            return View(await _context.BookOrders.Include(o => o.Book).ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int id, string status)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            var order = await _context.BookOrders.FindAsync(id);
            if (order != null)
            {
                order.Status = status;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Orders");
        }
        #endregion

        #region User Submissions
        public async Task<IActionResult> Submissions()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            return View(await _context.UserSubmissions.Include(s => s.User).ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> SelectWinner(int id, decimal prize)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            var sub = await _context.UserSubmissions.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == id);
            if (sub != null && sub.User != null)
            {
                sub.Status = "Selected";
                sub.PrizeMoney = prize;
                sub.EnlistedOnHome = true;
                
                // Generate a smart certificate URL using an external service
                var encodedName = Uri.EscapeDataString(sub.User.Name);
                var encodedRecipe = Uri.EscapeDataString(sub.RecipeName);
                sub.CertificateUrl = $"https://api.certifier.me/v1/certificate?name={encodedName}&course=Master%20Churner&date={DateTime.Now:yyyy-MM-dd}";
                
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Submissions");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateSubmissionImage(int id, IFormFile imageFile)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            var sub = await _context.UserSubmissions.FindAsync(id);
            if (sub != null && imageFile != null)
            {
                sub.ImageUrl = await SaveFile(imageFile, "submissions");
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Submissions");
        }
        #endregion

        #region User Management
        public async Task<IActionResult> Users()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            return View(await _context.Users.ToListAsync());
        }
        #endregion

        #region Feedback Management
        public async Task<IActionResult> Feedback()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            return View(await _context.Feedbacks.OrderByDescending(f => f.Date).ToListAsync());
        }
        #endregion
        #region Book Management
        public async Task<IActionResult> Books()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            return View(await _context.Books.ToListAsync());
        }

        [HttpGet]
        public IActionResult CreateBook() => IsAdmin() ? View() : RedirectToAction("Login", "Account");

        [HttpPost]
        public async Task<IActionResult> CreateBook(Book book)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            
            if (book.ImageFile != null)
            {
                book.ImageUrl = await SaveFile(book.ImageFile, "books");
            }

            _context.Books.Add(book);
            await _context.SaveChangesAsync();
            return RedirectToAction("Books");
        }

        [HttpGet]
        public async Task<IActionResult> EditBook(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            var book = await _context.Books.FindAsync(id);
            if (book == null) return NotFound();
            return View(book);
        }

        [HttpPost]
        public async Task<IActionResult> EditBook(Book book)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            
            var existing = await _context.Books.FindAsync(book.Id);
            if (existing == null) return NotFound();

            existing.Title = book.Title;
            existing.Description = book.Description;
            existing.Price = book.Price;

            if (book.ImageFile != null)
            {
                existing.ImageUrl = await SaveFile(book.ImageFile, "books");
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Books");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteBook(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            var book = await _context.Books.FindAsync(id);
            if (book != null)
            {
                _context.Books.Remove(book);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Books");
        }
        #endregion

        #region Deal Management
        public async Task<IActionResult> Deals()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            return View(await _context.Deals.ToListAsync());
        }

        [HttpGet]
        public IActionResult CreateDeal() => IsAdmin() ? View() : RedirectToAction("Login", "Account");

        [HttpPost]
        public async Task<IActionResult> CreateDeal(Deal deal)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            
            if (deal.ImageFile != null)
            {
                deal.ImageUrl = await SaveFile(deal.ImageFile, "deals");
            }

            _context.Deals.Add(deal);
            await _context.SaveChangesAsync();
            return RedirectToAction("Deals");
        }

        [HttpGet]
        public async Task<IActionResult> EditDeal(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            var deal = await _context.Deals.FindAsync(id);
            if (deal == null) return NotFound();
            return View(deal);
        }

        [HttpPost]
        public async Task<IActionResult> EditDeal(Deal deal)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            
            var existing = await _context.Deals.FindAsync(deal.Id);
            if (existing == null) return NotFound();

            existing.Title = deal.Title;
            existing.Tagline = deal.Tagline;
            existing.Description = deal.Description;
            existing.Price = deal.Price;
            existing.BadgeText = deal.BadgeText;
            existing.IsHighlighted = deal.IsHighlighted;

            if (deal.ImageFile != null)
            {
                existing.ImageUrl = await SaveFile(deal.ImageFile, "deals");
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Deals");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteDeal(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            var deal = await _context.Deals.FindAsync(id);
            if (deal != null)
            {
                _context.Deals.Remove(deal);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Deals");
        }
        #endregion

        #region HeroSlider Management
        public async Task<IActionResult> HeroSliders()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            var sliders = await _context.HeroSliders.OrderBy(s => s.DisplayOrder).ToListAsync();
            return View(sliders);
        }

        [HttpGet]
        public IActionResult CreateHeroSlider()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateHeroSlider(HeroSlider slider)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            
            if (slider.ImageFile != null)
            {
                slider.ImageUrl = await SaveFile(slider.ImageFile, "hero");
            }
            
            _context.HeroSliders.Add(slider);
            await _context.SaveChangesAsync();
            return RedirectToAction("HeroSliders");
        }

        [HttpGet]
        public async Task<IActionResult> EditHeroSlider(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            var slider = await _context.HeroSliders.FindAsync(id);
            if (slider == null) return NotFound();
            return View(slider);
        }

        [HttpPost]
        public async Task<IActionResult> EditHeroSlider(HeroSlider slider)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            
            var existing = await _context.HeroSliders.FindAsync(slider.Id);
            if (existing == null) return NotFound();

            existing.Title = slider.Title;
            existing.Subtitle = slider.Subtitle;
            existing.DisplayOrder = slider.DisplayOrder;

            if (slider.ImageFile != null)
            {
                existing.ImageUrl = await SaveFile(slider.ImageFile, "hero");
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("HeroSliders");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteHeroSlider(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            var slider = await _context.HeroSliders.FindAsync(id);
            if (slider != null)
            {
                _context.HeroSliders.Remove(slider);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("HeroSliders");
        }
        #endregion

        #region Helper Methods
        private async Task<string> SaveFile(IFormFile file, string folder)
        {
            // Upload to Cloudinary using the existing service
            return await _cloudinaryService.UploadImageAsync(file, folder);
        }
        #endregion
    }
}
