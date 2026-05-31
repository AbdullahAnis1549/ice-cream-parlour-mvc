using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication12.Data;
using WebApplication12.Models;
using WebApplication12.Services;

namespace WebApplication12.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly CloudinaryService _cloudinaryService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            ApplicationDbContext context,
            EmailService emailService,
            CloudinaryService cloudinaryService,
            ILogger<AccountController> logger)
        {
            _context = context;
            _emailService = emailService;
            _cloudinaryService = cloudinaryService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("UserId"))) return RedirectToAction("Index", "Home");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(ApplicationUser model, IFormFile? imageFile, string? CardNumber, string? CardExpiry, string? CardCvv)
        {
            if (model.SubscriptionType == "Monthly" || model.SubscriptionType == "Yearly")
            {
                if (string.IsNullOrEmpty(CardNumber) || string.IsNullOrEmpty(CardExpiry) || string.IsNullOrEmpty(CardCvv))
                {
                    ModelState.AddModelError("", "Payment details are required for paid memberships.");
                    return View(model);
                }

                try
                {
                    var expiryParts = CardExpiry.Split('/');
                    if (expiryParts.Length != 2)
                    {
                        ModelState.AddModelError("", "Invalid expiry date format. Use MM/YY.");
                        return View(model);
                    }

                    string sourceToken = "tok_visa";
                    if (CardNumber != null && CardNumber.StartsWith("5")) sourceToken = "tok_mastercard";
                    else if (CardNumber != null && CardNumber.StartsWith("3")) sourceToken = "tok_amex";

                    var chargeOptions = new Stripe.ChargeCreateOptions
                    {
                        Amount = model.SubscriptionType == "Monthly" ? 1500 : 15000,
                        Currency = "usd",
                        Source = sourceToken,
                        Description = $"Membership Payment - {model.SubscriptionType}",
                    };
                    var chargeService = new Stripe.ChargeService();
                    var charge = await chargeService.CreateAsync(chargeOptions);

                    if (charge.Status != "succeeded")
                    {
                        ModelState.AddModelError("", "Payment failed. Please try again.");
                        return View(model);
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Payment processing error: " + ex.Message);
                    return View(model);
                }
            }

            if (!ModelState.IsValid) return View(model);

            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == model.Email.ToLower()))
            {
                ModelState.AddModelError("Email", "Email already registered.");
                return View(model);
            }

            if (imageFile != null && imageFile.Length > 0)
            {
                try
                {
                    model.ImageUrl = await _cloudinaryService.UploadImageAsync(imageFile);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Image upload failed");
                    ModelState.AddModelError("", "Failed to upload image.");
                    return View(model);
                }
            }

            model.Password = BCrypt.Net.BCrypt.HashPassword(model.Password);
            model.VerifyStatus = false;
            model.VerifyCode = new Random().Next(100000, 999999);
            model.VerifyCodeExpDate = DateTime.UtcNow.AddMinutes(15);
            model.UserRole = "user"; 

            // Handle Subscription
            if (model.SubscriptionType == "Monthly")
                model.SubscriptionEndDate = DateTime.Now.AddMonths(1);
            else if (model.SubscriptionType == "Yearly")
                model.SubscriptionEndDate = DateTime.Now.AddYears(1);

            _context.Users.Add(model);
            await _context.SaveChangesAsync();

            try
            {
                _emailService.SendVerificationEmail(model.Email, model.VerifyCode.Value);
            }
            catch (Exception ex) { _logger.LogError(ex, "Verification email failed"); }

            TempData["UserEmail"] = model.Email;
            return RedirectToAction("VerifyUser");
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("UserId"))) return RedirectToAction("Index", "Home");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Email and password required");
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.Password))
            {
                ModelState.AddModelError("", "Invalid credentials");
                return View();
            }

            if (user.VerifyStatus != true)
            {
                TempData["UserEmail"] = user.Email;
                return RedirectToAction("VerifyUser");
            }

            HttpContext.Session.SetString("UserId", user.Id.ToString());
            HttpContext.Session.SetString("UserName", user.Name);
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserRole", user.UserRole ?? "user");
            HttpContext.Session.SetString("UserImageUrl", user.ImageUrl ?? "");

            if (user.UserRole == "admin")
            {
                return RedirectToAction("Dashboard", "Admin");
            }
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            TempData["SuccessMessage"] = "Logged out successfully.";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult VerifyUser()
        {
            var email = TempData["UserEmail"]?.ToString();
            if (string.IsNullOrEmpty(email)) return RedirectToAction("Register");
            ViewBag.Email = email;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyUser(string email, int verificationCode)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            if (user == null) return RedirectToAction("Register");

            if (user.VerifyCode != verificationCode || user.VerifyCodeExpDate < DateTime.UtcNow)
            {
                ModelState.AddModelError("", "Invalid or expired verification code");
                ViewBag.Email = email;
                return View();
            }

            user.VerifyStatus = true;
            user.VerifyCode = null;
            user.VerifyCodeExpDate = null;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Email verified. Please login.";
            return RedirectToAction("Login");
        }
        // ==================== PROFILE ====================
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login");

            int userId = int.Parse(userIdStr);
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return RedirectToAction("Login");
            
            ViewBag.Submissions = await _context.UserSubmissions.Where(s => s.UserId == userId).ToListAsync();
            ViewBag.Orders = await _context.BookOrders.Include(o => o.Book).Where(o => o.Email == user.Email).OrderByDescending(o => o.OrderDate).ToListAsync();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upgrade(string subscriptionType, string CardNumber, string CardExpiry, string CardCvv)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login");

            int userId = int.Parse(userIdStr);
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return RedirectToAction("Login");

            if (subscriptionType == "Monthly" || subscriptionType == "Yearly")
            {
                try
                {
                    var expiryParts = CardExpiry.Split('/');
                    if (expiryParts.Length != 2)
                    {
                        TempData["ErrorMessage"] = "Invalid expiry date format. Use MM/YY.";
                        return RedirectToAction("Profile");
                    }

                    string sourceToken = "tok_visa";
                    if (CardNumber != null && CardNumber.StartsWith("5")) sourceToken = "tok_mastercard";
                    else if (CardNumber != null && CardNumber.StartsWith("3")) sourceToken = "tok_amex";

                    var chargeOptions = new Stripe.ChargeCreateOptions
                    {
                        Amount = subscriptionType == "Monthly" ? 1500 : 15000,
                        Currency = "usd",
                        Source = sourceToken,
                        Description = $"Membership Upgrade - {subscriptionType}",
                    };
                    var chargeService = new Stripe.ChargeService();
                    var charge = await chargeService.CreateAsync(chargeOptions);

                    if (charge.Status == "succeeded")
                    {
                        user.SubscriptionType = subscriptionType;
                        if (subscriptionType == "Monthly")
                            user.SubscriptionEndDate = DateTime.Now.AddMonths(1);
                        else
                            user.SubscriptionEndDate = DateTime.Now.AddYears(1);

                        await _context.SaveChangesAsync();
                        TempData["SuccessMessage"] = "Membership upgraded successfully!";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Payment failed. Please try again.";
                    }
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Payment processing error: " + ex.Message;
                }
            }
            return RedirectToAction("Profile");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string name, string phone, IFormFile? imageFile)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login");

            int userId = int.Parse(userIdStr);
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return RedirectToAction("Login");

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "Name is required.";
                return RedirectToAction("Profile");
            }
            if (name.Length > 100)
            {
                TempData["ErrorMessage"] = "Name cannot exceed 100 characters.";
                return RedirectToAction("Profile");
            }
            user.Name = name;
            HttpContext.Session.SetString("UserName", name);

            if (string.IsNullOrWhiteSpace(phone))
            {
                TempData["ErrorMessage"] = "Phone number is required.";
                return RedirectToAction("Profile");
            }
            if (!System.Text.RegularExpressions.Regex.IsMatch(phone, @"^03\d{9}$"))
            {
                TempData["ErrorMessage"] = "Phone number must start with 03 and be exactly 11 digits.";
                return RedirectToAction("Profile");
            }
            user.Phone = phone;

            if (imageFile != null && imageFile.Length > 0)
            {
                try
                {
                    user.ImageUrl = await _cloudinaryService.UploadImageAsync(imageFile);
                    HttpContext.Session.SetString("UserImageUrl", user.ImageUrl);
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Failed to upload new profile image: " + ex.Message;
                    return RedirectToAction("Profile");
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Profile updated successfully!";
            return RedirectToAction("Profile");
        }

        // ==================== FORGOT PASSWORD ====================
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError("", "Please enter your email address.");
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            if (user == null)
            {
                ModelState.AddModelError("", "No account found with this email.");
                return View();
            }

            // Generate a 6-digit reset code
            var resetCode = new Random().Next(100000, 999999).ToString();
            user.ForgotCode = resetCode;
            user.ForgotCodeExp = DateTime.UtcNow.AddMinutes(15);
            await _context.SaveChangesAsync();

            try
            {
                _emailService.SendVerificationEmail(user.Email, int.Parse(resetCode));
            }
            catch (Exception ex) { _logger.LogError(ex, "Reset code email failed"); }

            TempData["ResetEmail"] = email;
            TempData["SuccessMessage"] = "A reset code has been sent to your email.";
            return RedirectToAction("ResetPassword");
        }

        [HttpGet]
        public IActionResult ResetPassword()
        {
            var email = TempData["ResetEmail"]?.ToString();
            if (string.IsNullOrEmpty(email)) return RedirectToAction("ForgotPassword");
            ViewBag.Email = email;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string email, string resetCode, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("", "Passwords do not match.");
                ViewBag.Email = email;
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            if (user == null)
            {
                ModelState.AddModelError("", "Invalid request.");
                return View();
            }

            if (user.ForgotCode != resetCode || user.ForgotCodeExp < DateTime.UtcNow)
            {
                ModelState.AddModelError("", "Invalid or expired reset code.");
                ViewBag.Email = email;
                return View();
            }

            user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.ForgotCode = null;
            user.ForgotCodeExp = null;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Password reset successfully. Please login with your new password.";
            return RedirectToAction("Login");
        }
    }
}
