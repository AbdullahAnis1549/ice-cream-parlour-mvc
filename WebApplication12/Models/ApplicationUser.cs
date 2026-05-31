using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication12.Models
{
    public class ApplicationUser
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email")]
        [GmailEmail(ErrorMessage = "Only Gmail accounts are allowed")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone is required")]
        [RegularExpression(@"^03\d{9}$", ErrorMessage = "Phone number must start with 03 and be exactly 11 digits.")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(255, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        [NotMapped]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmedPassword { get; set; } = string.Empty;

        public bool? VerifyStatus { get; set; }
        public int? VerifyCode { get; set; }
        public DateTime? VerifyCodeExpDate { get; set; }
        public string? ForgotCode { get; set; }
        public DateTime? ForgotCodeExp { get; set; }
        public string? ImageUrl { get; set; }
        public string UserRole { get; set; } = "user";

        public string? SubscriptionType { get; set; } // Monthly, Yearly
        public DateTime? SubscriptionEndDate { get; set; }
        public bool IsPaidMember => SubscriptionEndDate.HasValue && SubscriptionEndDate > DateTime.Now;

        public ApplicationUser()
        {
            VerifyStatus = false;
            UserRole = "user";
        }
    }
}
