using System;
using System.Collections.Generic;

namespace ClassificadorDoc.Models.ViewModels
{
    public class DashboardViewModel
    {
        public DashboardStats Stats { get; set; } = new();
        public List<RecentActivity> RecentActivities { get; set; } = new();
        public ChartData ChartData { get; set; } = new();
        public List<TypeStatistic> TypeStatistics { get; set; } = new();
    }

    public class DashboardStats
    {
        public int TotalDocuments { get; set; }
        public decimal SuccessRate { get; set; }
        public int ActiveUsers { get; set; }
        public int ProcessingCount { get; set; }
    }

    public class RecentActivity
    {
        public string FileName { get; set; } = string.Empty;
        public string Classification { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; }
        public string FileExtension { get; set; } = string.Empty;
        public string TimeAgo { get; set; } = string.Empty;
        public string BadgeClass { get; set; } = string.Empty;
        public string IconClass { get; set; } = string.Empty;
    }

    public class ChartData
    {
        public List<string> Labels { get; set; } = new();
        public List<int> Values { get; set; } = new();
    }

    public class TypeStatistic
    {
        public string Type { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Percentage { get; set; }
        public string ProgressBarClass { get; set; } = string.Empty;
    }
}
