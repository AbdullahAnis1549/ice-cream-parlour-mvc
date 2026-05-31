using System.ComponentModel.DataAnnotations;

namespace WebApplication12.Models
{
    public class GmailEmailAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return ValidationResult.Success;
            }

            string email = value.ToString()!;
            if (email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.Success;
            }

            return new ValidationResult(ErrorMessage ?? "Email must be a Gmail address (@gmail.com)");
        }
    }
}
