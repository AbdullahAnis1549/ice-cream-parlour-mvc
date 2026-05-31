using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication12.Models
{
    // Ice Cream Flavors / Recipes
    public class Recipe
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        [Required]
        public string Ingredients { get; set; } = string.Empty;
        [Required]
        public string Procedure { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public bool IsFree { get; set; } = false;

        [NotMapped]
        public IFormFile? ImageFile { get; set; }
    }

    // Recipe Books for sale
    public class Book
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }

        [NotMapped]
        public IFormFile? ImageFile { get; set; }
    }

    // Orders for Books
    public class BookOrder
    {
        [Key]
        public int Id { get; set; }
        [Required(ErrorMessage = "Full name is required")]
        public string CustomerName { get; set; } = string.Empty;
        [Required(ErrorMessage = "Email address is required"), EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;
        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(@"^03\d{9}$", ErrorMessage = "Phone number must start with 03 and be exactly 11 digits.")]
        public string Phone { get; set; } = string.Empty;
        [Required(ErrorMessage = "Delivery address is required")]
        public string Address { get; set; } = string.Empty;
        
        public int BookId { get; set; }
        [ForeignKey("BookId")]
        public virtual Book? Book { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost { get; set; }
        
        public string PaymentMethod { get; set; } = "Credit Card";
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public string Status { get; set; } = "Ordered"; // Ordered, Processed, Completed
    }

    // Feedback from users
    public class Feedback
    {
        [Key]
        public int Id { get; set; }
        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; } = string.Empty;
        [Required(ErrorMessage = "Email address is required"), EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;
        [Required(ErrorMessage = "Testimonial message is required")]
        public string Message { get; set; } = string.Empty;
        public DateTime Date { get; set; } = DateTime.Now;
    }

    // User Submitted Recipes
    public class UserSubmission
    {
        [Key]
        public int Id { get; set; }
        [Required(ErrorMessage = "Recipe title is required")]
        public string RecipeName { get; set; } = string.Empty;
        [Required(ErrorMessage = "Ingredients list is required")]
        public string Ingredients { get; set; } = string.Empty;
        [Required(ErrorMessage = "Preparation methodology is required")]
        public string Procedure { get; set; } = string.Empty;
        
        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }

        public string? Status { get; set; } = "Pending"; // Pending, Selected, Rejected
        public decimal? PrizeMoney { get; set; }
        public string? CertificateUrl { get; set; }
        public bool EnlistedOnHome { get; set; } = false;
        
        public string? ImageUrl { get; set; }
        
        [NotMapped]
        public IFormFile? ImageFile { get; set; }
    }
    // Promotional Deals
    public class Deal
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Title { get; set; } = string.Empty;
        public string? Tagline { get; set; }
        public string? Description { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }
        public string? BadgeText { get; set; } // e.g. "Best Value", "Save 15%"
        public string? ImageUrl { get; set; }
        public bool IsHighlighted { get; set; } = false;

        [NotMapped]
        public IFormFile? ImageFile { get; set; }
    }

    // Dynamic Hero Slider
    public class HeroSlider
    {
        [Key]
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Subtitle { get; set; }
        public string? ImageUrl { get; set; }
        public int DisplayOrder { get; set; } = 0;

        [NotMapped]
        public IFormFile? ImageFile { get; set; }
    }
}
