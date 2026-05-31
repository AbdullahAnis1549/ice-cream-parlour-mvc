using WebApplication12.Models;

namespace WebApplication12.ViewModels
{
    public class HomeViewModel
    {
        public IEnumerable<UserSubmission>? Winners { get; set; }
        public IEnumerable<Deal>? Deals { get; set; }
        public IEnumerable<HeroSlider>? Slides { get; set; }
    }
}
