using OutfitStudio.Models;

namespace OutfitStudio.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalProducts
        {
            get; set;
        }
        public int TotalOrders
        {
            get; set;
        }
        public int TotalUsers
        {
            get; set;
        }
        public decimal Revenue
        {
            get; set;
        }
        public int PendingOrders
        {
            get; set;
        }
        public int LowStockProducts
        {
            get; set;
        }
        public List<Order> RecentOrders { get; set; } = new();
        // Doanh thu 6 tháng gần nhất để vẽ biểu đồ: (nhãn tháng, doanh thu)
        public List<(string Label, decimal Value)> MonthlyRevenue { get; set; } = new();
    }

    public class ProductListViewModel
    {
        public List<Product> Products { get; set; } = new();
        public List<Category> Categories { get; set; } = new();
        public string? Search
        {
            get; set;
        }
        public int? CategoryId
        {
            get; set;
        }
    }

    public class ProductFormViewModel
    {
        public int InitialStock { get; set; }
        public Product Product { get; set; } = new();
        public List<Category> Categories { get; set; } = new();
    }
}


