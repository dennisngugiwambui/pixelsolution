namespace PixelSolution.ViewModels
{
    public class CategoryDetailsViewModel
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ProductCount { get; set; }
        public int ActiveProductCount { get; set; }
        public decimal TotalStockValue { get; set; }
    }
}
