using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpsDash.Domain.Entities;
using OpsDash.Domain.Enums;

namespace OpsDash.Infrastructure.Data.SeedData;

public static class DataSeeder
{
    private const int MetricBatchSize = 4000;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private static readonly string DemoPasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo@123");

    public static async Task SeedAsync(AppDbContext context)
    {
        if (await context.Tenants.AnyAsync())
        {
            return;
        }

        var utcNow = DateTime.UtcNow;

        var tenants = new[]
        {
            new Tenant
            {
                Name = "TechFlow Solutions",
                Subdomain = "techflow",
                Plan = "Enterprise",
                CreatedAt = utcNow,
                IsActive = true,
            },
            new Tenant
            {
                Name = "Metro Health Network",
                Subdomain = "metrohealth",
                Plan = "Professional",
                CreatedAt = utcNow,
                IsActive = true,
            },
            new Tenant
            {
                Name = "UrbanCart",
                Subdomain = "urbancart",
                Plan = "Enterprise",
                CreatedAt = utcNow,
                IsActive = true,
            },
        };

        context.Tenants.AddRange(tenants);
        await context.SaveChangesAsync();

        var adminPerms = JsonSerializer.Serialize(
            new[] { "users.manage", "metrics.manage", "alerts.manage", "incidents.manage", "reports.manage" },
            JsonOptions);
        var userPerms = JsonSerializer.Serialize(
            new[] { "metrics.view", "alerts.view", "incidents.view" },
            JsonOptions);
        var viewerPerms = JsonSerializer.Serialize(new[] { "metrics.view" }, JsonOptions);

        var roles = new List<Role>();
        foreach (var t in tenants)
        {
            roles.AddRange(
                new[]
                {
                    new Role { TenantId = t.Id, Name = "Admin", Permissions = adminPerms },
                    new Role { TenantId = t.Id, Name = "User", Permissions = userPerms },
                    new Role { TenantId = t.Id, Name = "Viewer", Permissions = viewerPerms },
                });
        }

        context.Roles.AddRange(roles);
        await context.SaveChangesAsync();

        var roleIds = await context.Roles.IgnoreQueryFilters()
            .ToDictionaryAsync(r => (r.TenantId, r.Name), r => r.Id);

        User BuildUser(int tenantId, string email, string firstName, string lastName, string roleName) =>
            new()
            {
                TenantId = tenantId,
                Email = email,
                PasswordHash = DemoPasswordHash,
                FirstName = firstName,
                LastName = lastName,
                RoleId = roleIds[(tenantId, roleName)],
                CreatedAt = utcNow,
                IsActive = true,
            };

        var users = new List<User>
        {
            BuildUser(tenants[0].Id, "admin@techflow.com", "Sarah", "Chen", "Admin"),
            BuildUser(tenants[0].Id, "user@techflow.com", "James", "Rodriguez", "User"),
            BuildUser(tenants[0].Id, "viewer@techflow.com", "Emily", "Watson", "Viewer"),
            BuildUser(tenants[0].Id, "ops@techflow.com", "Michael", "Park", "User"),
            BuildUser(tenants[1].Id, "admin@metrohealth.com", "Dr. Lisa", "Thompson", "Admin"),
            BuildUser(tenants[1].Id, "nurse@metrohealth.com", "Rachel", "Kim", "User"),
            BuildUser(tenants[1].Id, "analyst@metrohealth.com", "David", "Martinez", "User"),
            BuildUser(tenants[2].Id, "admin@urbancart.com", "Alex", "Johnson", "Admin"),
            BuildUser(tenants[2].Id, "ops@urbancart.com", "Priya", "Patel", "User"),
            BuildUser(tenants[2].Id, "analyst@urbancart.com", "Tom", "Wilson", "User"),
            BuildUser(tenants[2].Id, "viewer@urbancart.com", "Nina", "Brooks", "Viewer"),
        };

        context.Users.AddRange(users);
        await context.SaveChangesAsync();

        var allMetrics = new List<Metric>();
        allMetrics.AddRange(BuildTechFlowMetrics(tenants[0].Id));
        allMetrics.AddRange(BuildMetroMetrics(tenants[1].Id));
        allMetrics.AddRange(BuildUrbanMetrics(tenants[2].Id));

        foreach (var chunk in allMetrics.Chunk(MetricBatchSize))
        {
            context.Metrics.AddRange(chunk);
            await context.SaveChangesAsync();
        }

        var baselineRows = ComputeBaselines(allMetrics);
        context.MetricBaselines.AddRange(baselineRows);
        await context.SaveChangesAsync();

        var baselineLookup = baselineRows.ToDictionary(b => (b.TenantId, b.MetricName), b => b);

        var tfAdmin = users.First(u => u.Email == "admin@techflow.com").Id;
        var mhAdmin = users.First(u => u.Email == "admin@metrohealth.com").Id;
        var ucAdmin = users.First(u => u.Email == "admin@urbancart.com").Id;

        await SeedTechFlowStoryAsync(context, tenants[0].Id, allMetrics, baselineLookup, tfAdmin);
        await SeedMetroStoryAsync(context, tenants[1].Id, allMetrics, baselineLookup, mhAdmin);
        await SeedUrbanStoryAsync(context, tenants[2].Id, allMetrics, baselineLookup, ucAdmin);

        await SeedAlertRulesAndAlertsAsync(context, tenants, tfAdmin, mhAdmin, ucAdmin);
        await SeedHealthScoresAsync(context, tenants.Select(t => t.Id).ToArray(), utcNow);
    }

    private static List<Metric> BuildTechFlowMetrics(int tenantId)
    {
        var list = new List<Metric>();
        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "active_users",
                "Engagement",
                tenantId,
                1200m,
                0.15m,
                "hourly",
                new[]
                {
                    new AnomalyEvent { DaysAgo = 14, HourUtc = 11, IsSpike = false, VarianceMultiplier = 4.5 },
                    new AnomalyEvent { DaysAgo = 5, HourUtc = 13, IsSpike = false, VarianceMultiplier = 4.2 },
                },
                new MetricSeriesBehavior { WeekendDip = true, LinearTrendTotal = 0.06m }));

        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "monthly_revenue",
                "Financial",
                tenantId,
                85000m,
                0.05m,
                "daily",
                Array.Empty<AnomalyEvent>(),
                new MetricSeriesBehavior { LinearTrendTotal = 0.04m }));

        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "churn_rate",
                "Financial",
                tenantId,
                0.032m,
                0.20m,
                "daily",
                new[] { new AnomalyEvent { DaysAgo = 30, HourUtc = 12, IsSpike = true, VarianceMultiplier = 4.0 } },
                new MetricSeriesBehavior()));

        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "api_response_time",
                "Performance",
                tenantId,
                180m,
                0.25m,
                "hourly",
                new[]
                {
                    new AnomalyEvent { DaysAgo = 75, HourUtc = 15, IsSpike = true, VarianceMultiplier = 4.2 },
                    new AnomalyEvent { DaysAgo = 45, HourUtc = 14, IsSpike = true, VarianceMultiplier = 4.5 },
                    new AnomalyEvent { DaysAgo = 5, HourUtc = 13, IsSpike = true, VarianceMultiplier = 4.0 },
                },
                new MetricSeriesBehavior()));

        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "error_rate",
                "Performance",
                tenantId,
                0.015m,
                0.30m,
                "hourly",
                new[]
                {
                    new AnomalyEvent { DaysAgo = 60, HourUtc = 16, IsSpike = true, VarianceMultiplier = 4.3 },
                    new AnomalyEvent { DaysAgo = 45, HourUtc = 14, IsSpike = true, VarianceMultiplier = 4.4 },
                    new AnomalyEvent { DaysAgo = 5, HourUtc = 13, IsSpike = true, VarianceMultiplier = 4.1 },
                    new AnomalyEvent { DaysAgo = 1, HourUtc = 10, IsSpike = true, VarianceMultiplier = 4.2 },
                },
                new MetricSeriesBehavior { MinValue = 0.0001m }));

        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "support_tickets",
                "Operations",
                tenantId,
                45m,
                0.20m,
                "daily",
                Array.Empty<AnomalyEvent>(),
                new MetricSeriesBehavior { WeekendDip = true, WeekendDipFactor = 0.85 }));

        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "deployment_frequency",
                "Operations",
                tenantId,
                3.2m,
                0.25m,
                "daily",
                Array.Empty<AnomalyEvent>(),
                new MetricSeriesBehavior { WeekdayOnlyDeployments = true }));

        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "customer_satisfaction",
                "Engagement",
                tenantId,
                4.2m,
                0.05m,
                "daily",
                Array.Empty<AnomalyEvent>(),
                new MetricSeriesBehavior { LinearTrendTotal = -0.08m, MinValue = 1m, MaxValue = 5m }));

        return list;
    }

    private static List<Metric> BuildMetroMetrics(int tenantId)
    {
        var list = new List<Metric>();
        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "er_wait_time",
                "Patient Care",
                tenantId,
                42m,
                0.30m,
                "hourly",
                new[]
                {
                    new AnomalyEvent { DaysAgo = 70, HourUtc = 18, IsSpike = true, VarianceMultiplier = 4.2 },
                    new AnomalyEvent { DaysAgo = 12, HourUtc = 19, IsSpike = true, VarianceMultiplier = 3.8 },
                },
                new MetricSeriesBehavior { WeekendSpikeMetric = true }));

        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "bed_occupancy",
                "Capacity",
                tenantId,
                0.78m,
                0.10m,
                "hourly",
                new[]
                {
                    new AnomalyEvent { DaysAgo = 55, HourUtc = 20, IsSpike = true, VarianceMultiplier = 3.5 },
                    new AnomalyEvent { DaysAgo = 12, HourUtc = 19, IsSpike = true, VarianceMultiplier = 3.6 },
                },
                new MetricSeriesBehavior { LinearTrendTotal = 0.07m, MinValue = 0.05m, MaxValue = 1m }));

        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "patient_throughput",
                "Patient Care",
                tenantId,
                95m,
                0.15m,
                "daily",
                Array.Empty<AnomalyEvent>(),
                new MetricSeriesBehavior { WeekdayHigher = true }));

        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "staffing_ratio",
                "Staffing",
                tenantId,
                0.85m,
                0.08m,
                "hourly",
                new[]
                {
                    new AnomalyEvent { DaysAgo = 40, HourUtc = 9, IsSpike = false, VarianceMultiplier = 3.9 },
                    new AnomalyEvent { DaysAgo = 1, HourUtc = 8, IsSpike = false, VarianceMultiplier = 4.0 },
                },
                new MetricSeriesBehavior { WeekendDip = true, WeekendDipFactor = 0.88, MinValue = 0.2m, MaxValue = 1m }));

        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "readmission_rate",
                "Quality",
                tenantId,
                0.12m,
                0.15m,
                "daily",
                new[] { new AnomalyEvent { DaysAgo = 40, HourUtc = 12, IsSpike = true, VarianceMultiplier = 3.7 } },
                new MetricSeriesBehavior { MinValue = 0.01m, MaxValue = 0.5m }));

        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "appointment_no_shows",
                "Quality",
                tenantId,
                0.08m,
                0.25m,
                "daily",
                new[] { new AnomalyEvent { DaysAgo = 25, HourUtc = 12, IsSpike = true, VarianceMultiplier = 4.1 } },
                new MetricSeriesBehavior { MondaySpike = true, MinValue = 0.01m, MaxValue = 0.35m }));

        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "avg_treatment_time",
                "Patient Care",
                tenantId,
                35m,
                0.20m,
                "hourly",
                Array.Empty<AnomalyEvent>(),
                new MetricSeriesBehavior()));

        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "patient_satisfaction",
                "Quality",
                tenantId,
                3.8m,
                0.08m,
                "daily",
                new[] { new AnomalyEvent { DaysAgo = 3, HourUtc = 12, IsSpike = false, VarianceMultiplier = 3.2 } },
                new MetricSeriesBehavior { LinearTrendTotal = -0.05m, MinValue = 1m, MaxValue = 5m }));

        return list;
    }

    private static List<Metric> BuildUrbanMetrics(int tenantId)
    {
        var list = new List<Metric>();
        var checkout = MetricSeriesGenerator.GenerateMetricSeries(
            "checkout_conversion",
            "Conversion",
            tenantId,
            0.034m,
            0.15m,
            "hourly",
            new[] { new AnomalyEvent { DaysAgo = 28, HourUtc = 15, IsSpike = false, VarianceMultiplier = 3.8 } },
            new MetricSeriesBehavior { WeekendRise = true, MinValue = 0.005m, MaxValue = 0.08m });
        list.AddRange(checkout);

        list.AddRange(
            MetricSeriesGenerator.GenerateCartAbandonmentFromCheckout(
                checkout,
                tenantId,
                "Conversion",
                new[]
                {
                    new AnomalyEvent { DaysAgo = 15, HourUtc = 16, IsSpike = true, VarianceMultiplier = 3.5 },
                },
                new MetricSeriesBehavior { WeekendRise = true }));

        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "page_load_time",
                "Performance",
                tenantId,
                2.1m,
                0.30m,
                "hourly",
                new[]
                {
                    new AnomalyEvent { DaysAgo = 72, HourUtc = 17, IsSpike = true, VarianceMultiplier = 4.0 },
                    new AnomalyEvent { DaysAgo = 44, HourUtc = 18, IsSpike = true, VarianceMultiplier = 4.2 },
                    new AnomalyEvent { DaysAgo = 1, HourUtc = 9, IsSpike = true, VarianceMultiplier = 4.3 },
                },
                new MetricSeriesBehavior { MinValue = 0.2m }));

        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "payment_success_rate",
                "Performance",
                tenantId,
                0.987m,
                0.02m,
                "hourly",
                new[]
                {
                    new AnomalyEvent { DaysAgo = 58, HourUtc = 11, IsSpike = false, VarianceMultiplier = 4.0 },
                    new AnomalyEvent { DaysAgo = 44, HourUtc = 18, IsSpike = false, VarianceMultiplier = 3.9 },
                },
                new MetricSeriesBehavior { MinValue = 0.85m, MaxValue = 1m }));

        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "order_fulfillment_time",
                "Fulfillment",
                tenantId,
                28m,
                0.20m,
                "daily",
                Array.Empty<AnomalyEvent>(),
                new MetricSeriesBehavior { LinearTrendTotal = -0.07m, MinValue = 4m }));

        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "return_rate",
                "Fulfillment",
                tenantId,
                0.065m,
                0.15m,
                "daily",
                Array.Empty<AnomalyEvent>(),
                new MetricSeriesBehavior { MinValue = 0.01m, MaxValue = 0.2m }));

        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "avg_order_value",
                "Financial",
                tenantId,
                67m,
                0.12m,
                "daily",
                Array.Empty<AnomalyEvent>(),
                new MetricSeriesBehavior { WeekendRise = true }));

        list.AddRange(
            MetricSeriesGenerator.GenerateMetricSeries(
                "daily_revenue",
                "Financial",
                tenantId,
                125000m,
                0.18m,
                "daily",
                new[] { new AnomalyEvent { DaysAgo = 6, HourUtc = 12, IsSpike = false, VarianceMultiplier = 3.6 } },
                new MetricSeriesBehavior { WeekendRise = true, LinearTrendTotal = 0.05m }));

        return list;
    }

    private static List<MetricBaseline> ComputeBaselines(IEnumerable<Metric> metrics)
    {
        var groups = metrics.GroupBy(m => new { m.TenantId, m.MetricName });
        var list = new List<MetricBaseline>();
        foreach (var g in groups)
        {
            var ordered = g.OrderBy(x => x.RecordedAt).Select(x => x.MetricValue).ToList();
            var n = ordered.Count;
            if (n == 0)
            {
                continue;
            }

            var mean = ordered.Average();
            var variance = ordered.Sum(x => (x - mean) * (x - mean)) / Math.Max(n, 1);
            var std = (decimal)Math.Sqrt((double)variance);
            if (std == 0)
            {
                std = 0.0001m;
            }

            var third = Math.Max(1, n / 3);
            var early = ordered.Take(third).Average();
            var late = ordered.Skip(n - third).Average();
            var trend = late > early * 1.02m ? "Up" : late < early * 0.98m ? "Down" : "Stable";

            list.Add(
                new MetricBaseline
                {
                    TenantId = g.Key.TenantId,
                    MetricName = g.Key.MetricName,
                    Mean = decimal.Round(mean, 6),
                    StandardDeviation = decimal.Round(std, 6),
                    TrendDirection = trend,
                    DataPointCount = n,
                    LastCalculatedAt = DateTime.UtcNow,
                });
        }

        return list;
    }

    private static string SeverityFromZ(decimal z)
    {
        var a = Math.Abs(z);
        if (a < 2.5m)
        {
            return "Warning";
        }

        return a < 3.0m ? "Critical" : "Severe";
    }

    private static Metric? FindMetric(IReadOnlyList<Metric> metrics, int tenantId, string name, int daysAgo, int hourUtc, bool hourly)
    {
        var anchor = AnchorUtc(daysAgo, hourUtc, hourly);
        return metrics.Where(m => m.TenantId == tenantId && m.MetricName == name)
            .OrderBy(m => Math.Abs((m.RecordedAt - anchor).Ticks))
            .FirstOrDefault(m => Math.Abs((m.RecordedAt - anchor).TotalMinutes) <= 90);
    }

    private static DateTime AnchorUtc(int daysAgo, int hourUtc, bool hourly)
    {
        var day = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-daysAgo), DateTimeKind.Utc);
        return hourly ? day.AddHours(hourUtc) : day.AddHours(12);
    }

    private static async Task SeedTechFlowStoryAsync(
        AppDbContext context,
        int tenantId,
        List<Metric> metrics,
        IReadOnlyDictionary<(int TenantId, string MetricName), MetricBaseline> baselines,
        int adminUserId)
    {
        var plans = new[]
        {
            new AnomalyPlan("api_response_time", 75, 15, true, 3.6m, false, 2, "tf_i1"),
            new AnomalyPlan("error_rate", 60, 16, true, 3.2m, false, 2, "tf_i2"),
            new AnomalyPlan("api_response_time", 45, 14, true, 3.9m, false, 2, "tf_i3"),
            new AnomalyPlan("error_rate", 45, 14, true, 3.7m, false, 2, "tf_i3"),
            new AnomalyPlan("churn_rate", 30, 12, false, 3.4m, false, 3, "tf_i4"),
            new AnomalyPlan("active_users", 14, 11, true, 3.1m, false, 2, "tf_i5"),
            new AnomalyPlan("api_response_time", 5, 13, true, 4.2m, false, 1, "tf_i6"),
            new AnomalyPlan("error_rate", 5, 13, true, 3.8m, false, 1, "tf_i6"),
            new AnomalyPlan("active_users", 5, 13, true, 3.5m, true, null, "tf_i6"),
            new AnomalyPlan("error_rate", 1, 10, true, 4.1m, true, null, "tf_i7"),
        };

        await SeedTenantStoryCoreAsync(context, tenantId, metrics, baselines, adminUserId, plans, BuildTechFlowIncidents(), "tf");
    }

    private static async Task SeedMetroStoryAsync(
        AppDbContext context,
        int tenantId,
        List<Metric> metrics,
        IReadOnlyDictionary<(int TenantId, string MetricName), MetricBaseline> baselines,
        int adminUserId)
    {
        var plans = new[]
        {
            new AnomalyPlan("er_wait_time", 70, 18, true, 3.5m, false, 2, "mh_i1"),
            new AnomalyPlan("bed_occupancy", 55, 20, true, 3.3m, false, 2, "mh_i2"),
            new AnomalyPlan("staffing_ratio", 40, 9, true, 3.4m, false, 2, "mh_i3"),
            new AnomalyPlan("readmission_rate", 40, 12, false, 3.6m, false, 2, "mh_i3"),
            new AnomalyPlan("appointment_no_shows", 25, 12, false, 3.2m, false, 2, "mh_i4"),
            new AnomalyPlan("er_wait_time", 12, 19, true, 3.7m, false, 2, "mh_i5"),
            new AnomalyPlan("bed_occupancy", 12, 19, true, 3.4m, false, 2, "mh_i5"),
            new AnomalyPlan("patient_satisfaction", 3, 12, false, 2.9m, true, null, "mh_i6"),
            new AnomalyPlan("staffing_ratio", 1, 8, true, 3.9m, true, null, "mh_i7"),
        };

        await SeedTenantStoryCoreAsync(context, tenantId, metrics, baselines, adminUserId, plans, BuildMetroIncidents(), "mh");
    }

    private static async Task SeedUrbanStoryAsync(
        AppDbContext context,
        int tenantId,
        List<Metric> metrics,
        IReadOnlyDictionary<(int TenantId, string MetricName), MetricBaseline> baselines,
        int adminUserId)
    {
        var plans = new[]
        {
            new AnomalyPlan("page_load_time", 72, 17, true, 3.5m, false, 2, "uc_i1"),
            new AnomalyPlan("payment_success_rate", 58, 11, true, 3.4m, false, 2, "uc_i2"),
            new AnomalyPlan("page_load_time", 44, 18, true, 3.8m, false, 2, "uc_i3"),
            new AnomalyPlan("payment_success_rate", 44, 18, true, 3.6m, false, 2, "uc_i3"),
            new AnomalyPlan("checkout_conversion", 28, 15, true, 3.2m, false, 2, "uc_i4"),
            new AnomalyPlan("cart_abandonment", 15, 16, true, 3.3m, false, 2, "uc_i5"),
            new AnomalyPlan("daily_revenue", 6, 12, false, 3.5m, false, 1, "uc_i6"),
            new AnomalyPlan("checkout_conversion", 6, 14, true, 3.1m, false, 1, "uc_i6"),
            new AnomalyPlan("page_load_time", 1, 9, true, 4.2m, true, null, "uc_i7"),
        };

        await SeedTenantStoryCoreAsync(context, tenantId, metrics, baselines, adminUserId, plans, BuildUrbanIncidents(), "uc");
    }

    private sealed record AnomalyPlan(
        string MetricName,
        int DaysAgo,
        int HourUtc,
        bool IsHourly,
        decimal ZScore,
        bool IsActive,
        int? ResolvedDaysAfter,
        string IncidentGroup);

    private sealed record IncidentBlueprint(
        string Key,
        string Title,
        string Status,
        string Severity,
        int StartedDaysAgo,
        string[] MetricNames,
        bool AddCorrelationEvent);

    private static async Task SeedTenantStoryCoreAsync(
        AppDbContext context,
        int tenantId,
        List<Metric> metrics,
        IReadOnlyDictionary<(int TenantId, string MetricName), MetricBaseline> baselines,
        int adminUserId,
        AnomalyPlan[] plans,
        IncidentBlueprint[] blueprints,
        string correlationProfile)
    {
        var metricSlice = metrics.Where(m => m.TenantId == tenantId).ToList();
        var anomalies = new List<AnomalyScore>();
        var anomalyByPlanKey = new Dictionary<(string Group, string Name, int Days, int Hour), AnomalyScore>();

        foreach (var p in plans)
        {
            var m = FindMetric(metricSlice, tenantId, p.MetricName, p.DaysAgo, p.HourUtc, p.IsHourly);
            if (m is null)
            {
                continue;
            }

            var bl = baselines[(tenantId, p.MetricName)];
            var detectedAt = m.RecordedAt.AddMinutes(4);
            DateTime? resolvedAt = null;
            if (!p.IsActive && p.ResolvedDaysAfter is { } d)
            {
                resolvedAt = detectedAt.AddDays(d);
            }

            var a = new AnomalyScore
            {
                TenantId = tenantId,
                MetricId = m.Id,
                MetricName = p.MetricName,
                MetricValue = m.MetricValue,
                ZScore = p.ZScore,
                Severity = SeverityFromZ(p.ZScore),
                BaselineMean = bl.Mean,
                BaselineStdDev = bl.StandardDeviation,
                DetectedAt = detectedAt,
                IsActive = p.IsActive,
                ResolvedAt = resolvedAt,
            };
            anomalies.Add(a);
            anomalyByPlanKey[(p.IncidentGroup, p.MetricName, p.DaysAgo, p.HourUtc)] = a;
        }

        var incidentByKey = new Dictionary<string, Incident>();
        foreach (var bp in blueprints)
        {
            var groupAnomalies = plans
                .Where(pl => pl.IncidentGroup == bp.Key)
                .Select(pl => anomalyByPlanKey.GetValueOrDefault((pl.IncidentGroup, pl.MetricName, pl.DaysAgo, pl.HourUtc)))
                .Where(a => a is not null)
                .Cast<AnomalyScore>()
                .ToList();
            if (groupAnomalies.Count == 0)
            {
                continue;
            }

            var sevOrder = new[] { "Warning", "Critical", "Severe" };
            var maxSev = groupAnomalies.OrderByDescending(a => Array.IndexOf(sevOrder, a.Severity)).First().Severity;
            var started = groupAnomalies.Min(a => a.DetectedAt).AddMinutes(-30);

            var inc = new Incident
            {
                TenantId = tenantId,
                Title = bp.Title,
                Severity = maxSev,
                Status = bp.Status,
                AnomalyCount = groupAnomalies.Count,
                AffectedMetrics = JsonSerializer.Serialize(bp.MetricNames, JsonOptions),
                StartedAt = started,
                AcknowledgedBy = bp.Status == nameof(IncidentStatus.Resolved) ? adminUserId : null,
                AcknowledgedAt = bp.Status == nameof(IncidentStatus.Resolved) ? started.AddMinutes(20) : null,
                ResolvedBy = bp.Status == nameof(IncidentStatus.Resolved) ? adminUserId : null,
                ResolvedAt = bp.Status == nameof(IncidentStatus.Resolved) ? started.AddDays(2) : null,
            };
            context.Incidents.Add(inc);
            incidentByKey[bp.Key] = inc;
        }

        await context.SaveChangesAsync();

        foreach (var bp in blueprints)
        {
            if (!incidentByKey.TryGetValue(bp.Key, out var inc))
            {
                continue;
            }

            foreach (var pl in plans.Where(p => p.IncidentGroup == bp.Key))
            {
                if (anomalyByPlanKey.TryGetValue((pl.IncidentGroup, pl.MetricName, pl.DaysAgo, pl.HourUtc), out var an))
                {
                    an.IncidentId = inc.Id;
                }
            }
        }

        context.AnomalyScores.AddRange(anomalies);
        await context.SaveChangesAsync();

        await AddCorrelationsAsync(context, tenantId, correlationProfile, plans, anomalies);

        await AddIncidentEventsAsync(context, tenantId, adminUserId, blueprints, anomalies);
    }

    private static IncidentBlueprint[] BuildTechFlowIncidents() =>
        new[]
        {
            new IncidentBlueprint("tf_i1", "API Performance Degradation", nameof(IncidentStatus.Resolved), "Severe", 75, new[] { "api_response_time" }, false),
            new IncidentBlueprint("tf_i2", "Error Rate Spike", nameof(IncidentStatus.Resolved), "Critical", 60, new[] { "error_rate" }, false),
            new IncidentBlueprint("tf_i3", "Correlated API and Error Spike", nameof(IncidentStatus.Resolved), "Severe", 45, new[] { "api_response_time", "error_rate" }, true),
            new IncidentBlueprint("tf_i4", "Customer Churn Increase", nameof(IncidentStatus.Resolved), "Critical", 30, new[] { "churn_rate" }, false),
            new IncidentBlueprint("tf_i5", "User Engagement Drop", nameof(IncidentStatus.Resolved), "Critical", 14, new[] { "active_users" }, false),
            new IncidentBlueprint("tf_i6", "Critical Service Degradation", nameof(IncidentStatus.Investigating), "Severe", 5, new[] { "api_response_time", "error_rate", "active_users" }, true),
            new IncidentBlueprint("tf_i7", "Elevated Error Rate", nameof(IncidentStatus.Open), "Severe", 1, new[] { "error_rate" }, false),
        };

    private static IncidentBlueprint[] BuildMetroIncidents() =>
        new[]
        {
            new IncidentBlueprint("mh_i1", "ER Wait Time Surge", nameof(IncidentStatus.Resolved), "Critical", 70, new[] { "er_wait_time" }, false),
            new IncidentBlueprint("mh_i2", "Bed Occupancy Pressure", nameof(IncidentStatus.Resolved), "Critical", 55, new[] { "bed_occupancy" }, false),
            new IncidentBlueprint("mh_i3", "Staffing and Readmission Correlation", nameof(IncidentStatus.Resolved), "Severe", 40, new[] { "staffing_ratio", "readmission_rate" }, true),
            new IncidentBlueprint("mh_i4", "No-Show Spike", nameof(IncidentStatus.Resolved), "Critical", 25, new[] { "appointment_no_shows" }, false),
            new IncidentBlueprint("mh_i5", "Capacity Strain Event", nameof(IncidentStatus.Resolved), "Severe", 12, new[] { "er_wait_time", "bed_occupancy" }, true),
            new IncidentBlueprint("mh_i6", "Patient Satisfaction Dip", nameof(IncidentStatus.Open), "Critical", 3, new[] { "patient_satisfaction" }, false),
            new IncidentBlueprint("mh_i7", "Low Staffing Ratio", nameof(IncidentStatus.Open), "Severe", 1, new[] { "staffing_ratio" }, false),
        };

    private static IncidentBlueprint[] BuildUrbanIncidents() =>
        new[]
        {
            new IncidentBlueprint("uc_i1", "Checkout Latency Incident", nameof(IncidentStatus.Resolved), "Critical", 72, new[] { "page_load_time" }, false),
            new IncidentBlueprint("uc_i2", "Payment Success Degradation", nameof(IncidentStatus.Resolved), "Critical", 58, new[] { "payment_success_rate" }, false),
            new IncidentBlueprint("uc_i3", "Correlated Latency and Payments", nameof(IncidentStatus.Resolved), "Severe", 44, new[] { "page_load_time", "payment_success_rate" }, true),
            new IncidentBlueprint("uc_i4", "Conversion Drop", nameof(IncidentStatus.Resolved), "Critical", 28, new[] { "checkout_conversion" }, false),
            new IncidentBlueprint("uc_i5", "Cart Abandonment Spike", nameof(IncidentStatus.Resolved), "Critical", 15, new[] { "cart_abandonment" }, false),
            new IncidentBlueprint("uc_i6", "Revenue and Conversion Risk", nameof(IncidentStatus.Investigating), "Severe", 6, new[] { "daily_revenue", "checkout_conversion" }, true),
            new IncidentBlueprint("uc_i7", "Page Load Regression", nameof(IncidentStatus.Open), "Severe", 1, new[] { "page_load_time" }, false),
        };

    private static async Task AddCorrelationsAsync(
        AppDbContext context,
        int tenantId,
        string correlationProfile,
        AnomalyPlan[] plans,
        List<AnomalyScore> anomalies)
    {
        AnomalyScore? Find(AnomalyPlan p) =>
            anomalies.FirstOrDefault(
                a => a.TenantId == tenantId
                    && a.MetricName == p.MetricName
                    && Math.Abs((a.DetectedAt - AnchorUtc(p.DaysAgo, p.HourUtc, p.IsHourly).AddMinutes(4)).TotalMinutes) < 120);

        var cors = new List<MetricCorrelation>();

        void Pair(AnomalyPlan a, AnomalyPlan b, int offsetSeconds)
        {
            var sa = Find(a);
            var sb = Find(b);
            if (sa is null || sb is null)
            {
                return;
            }

            cors.Add(
                new MetricCorrelation
                {
                    TenantId = tenantId,
                    SourceAnomalyId = sa.Id,
                    CorrelatedMetricName = sb.MetricName,
                    CorrelatedMetricValue = sb.MetricValue,
                    CorrelatedZScore = sb.ZScore,
                    TimeOffsetSeconds = offsetSeconds,
                    DetectedAt = sb.DetectedAt,
                });
        }

        switch (correlationProfile)
        {
            case "tf":
                Pair(
                    plans.First(p => p.MetricName == "api_response_time" && p.DaysAgo == 45),
                    plans.First(p => p.MetricName == "error_rate" && p.DaysAgo == 45),
                    240);
                Pair(
                    plans.First(p => p.MetricName == "api_response_time" && p.DaysAgo == 5),
                    plans.First(p => p.MetricName == "error_rate" && p.DaysAgo == 5),
                    180);
                break;
            case "mh":
                Pair(
                    plans.First(p => p.MetricName == "staffing_ratio" && p.DaysAgo == 40),
                    plans.First(p => p.MetricName == "readmission_rate" && p.DaysAgo == 40),
                    420);
                Pair(
                    plans.First(p => p.MetricName == "er_wait_time" && p.DaysAgo == 12),
                    plans.First(p => p.MetricName == "bed_occupancy" && p.DaysAgo == 12),
                    300);
                break;
            case "uc":
                Pair(
                    plans.First(p => p.MetricName == "page_load_time" && p.DaysAgo == 44),
                    plans.First(p => p.MetricName == "payment_success_rate" && p.DaysAgo == 44),
                    360);
                Pair(
                    plans.First(p => p.MetricName == "daily_revenue" && p.DaysAgo == 6),
                    plans.First(p => p.MetricName == "checkout_conversion" && p.DaysAgo == 6),
                    540);
                break;
        }

        if (cors.Count > 0)
        {
            context.MetricCorrelations.AddRange(cors);
            await context.SaveChangesAsync();
        }
    }

    private static async Task AddIncidentEventsAsync(
        AppDbContext context,
        int tenantId,
        int adminUserId,
        IncidentBlueprint[] blueprints,
        List<AnomalyScore> anomalies)
    {
        var incidents = await context.Incidents.IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId)
            .OrderBy(i => i.Id)
            .ToListAsync();

        var events = new List<IncidentEvent>();
        foreach (var inc in incidents)
        {
            var bp = blueprints.FirstOrDefault(b => b.Title == inc.Title);
            if (bp is null)
            {
                continue;
            }

            var groupAnomalies = anomalies.Where(a => a.IncidentId == inc.Id).OrderBy(a => a.DetectedAt).ToList();
            var t = inc.StartedAt;

            foreach (var a in groupAnomalies)
            {
                events.Add(
                    new IncidentEvent
                    {
                        IncidentId = inc.Id,
                        TenantId = tenantId,
                        EventType = nameof(EventType.AnomalyDetected),
                        Description = $"Anomaly detected on {a.MetricName} (Z={a.ZScore:0.##}).",
                        MetricName = a.MetricName,
                        MetricValue = a.MetricValue,
                        CreatedBy = null,
                        CreatedAt = a.DetectedAt,
                    });
            }

            if (bp.AddCorrelationEvent && groupAnomalies.Count >= 2)
            {
                events.Add(
                    new IncidentEvent
                    {
                        IncidentId = inc.Id,
                        TenantId = tenantId,
                        EventType = nameof(EventType.CorrelationFound),
                        Description = $"Correlation detected across metrics: {string.Join(", ", bp.MetricNames)}.",
                        CreatedBy = null,
                        CreatedAt = t.AddMinutes(25),
                    });
            }

            if (inc.Status == nameof(IncidentStatus.Resolved))
            {
                events.Add(
                    new IncidentEvent
                    {
                        IncidentId = inc.Id,
                        TenantId = tenantId,
                        EventType = nameof(EventType.Acknowledged),
                        Description = "Incident acknowledged by on-call admin.",
                        CreatedBy = adminUserId,
                        CreatedAt = inc.AcknowledgedAt ?? t.AddMinutes(30),
                    });
                events.Add(
                    new IncidentEvent
                    {
                        IncidentId = inc.Id,
                        TenantId = tenantId,
                        EventType = nameof(EventType.StatusChanged),
                        Description = $"Status changed to {nameof(IncidentStatus.Acknowledged)}.",
                        CreatedBy = adminUserId,
                        CreatedAt = t.AddMinutes(35),
                    });
                events.Add(
                    new IncidentEvent
                    {
                        IncidentId = inc.Id,
                        TenantId = tenantId,
                        EventType = nameof(EventType.StatusChanged),
                        Description = $"Status changed to {nameof(IncidentStatus.Investigating)}.",
                        CreatedBy = adminUserId,
                        CreatedAt = t.AddMinutes(50),
                    });
                events.Add(
                    new IncidentEvent
                    {
                        IncidentId = inc.Id,
                        TenantId = tenantId,
                        EventType = nameof(EventType.MetricNormalized),
                        Description = "Primary metrics returned to expected ranges.",
                        CreatedBy = null,
                        CreatedAt = t.AddHours(8),
                    });
                events.Add(
                    new IncidentEvent
                    {
                        IncidentId = inc.Id,
                        TenantId = tenantId,
                        EventType = nameof(EventType.Resolved),
                        Description = "Root cause addressed; monitoring continues.",
                        CreatedBy = adminUserId,
                        CreatedAt = inc.ResolvedAt ?? t.AddDays(2),
                    });
            }
            else if (inc.Status == nameof(IncidentStatus.Investigating))
            {
                events.Add(
                    new IncidentEvent
                    {
                        IncidentId = inc.Id,
                        TenantId = tenantId,
                        EventType = nameof(EventType.Acknowledged),
                        Description = "Incident acknowledged; war room active.",
                        CreatedBy = adminUserId,
                        CreatedAt = t.AddMinutes(15),
                    });
                events.Add(
                    new IncidentEvent
                    {
                        IncidentId = inc.Id,
                        TenantId = tenantId,
                        EventType = nameof(EventType.StatusChanged),
                        Description = $"Status changed to {nameof(IncidentStatus.Investigating)}.",
                        CreatedBy = adminUserId,
                        CreatedAt = t.AddMinutes(40),
                    });
            }
            else
            {
                events.Add(
                    new IncidentEvent
                    {
                        IncidentId = inc.Id,
                        TenantId = tenantId,
                        EventType = nameof(EventType.StatusChanged),
                        Description = $"Incident remains {nameof(IncidentStatus.Open)}.",
                        CreatedBy = null,
                        CreatedAt = t.AddMinutes(10),
                    });
            }
        }

        if (events.Count > 0)
        {
            context.IncidentEvents.AddRange(events);
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedAlertRulesAndAlertsAsync(
        AppDbContext context,
        Tenant[] tenants,
        int tfAdmin,
        int mhAdmin,
        int ucAdmin)
    {
        var rules = new List<AlertRule>();
        var t0 = tenants[0].Id;
        var t1 = tenants[1].Id;
        var t2 = tenants[2].Id;

        void R(int tenant, int admin, string metric, decimal th, string op, string mode, int? horizon) =>
            rules.Add(
                new AlertRule
                {
                    TenantId = tenant,
                    MetricName = metric,
                    Threshold = th,
                    Operator = op,
                    AlertMode = mode,
                    ForecastHorizon = horizon,
                    IsActive = true,
                    CreatedBy = admin,
                    CreatedAt = DateTime.UtcNow,
                });

        R(t0, tfAdmin, "error_rate", 0.05m, "GreaterThan", "Current", null);
        R(t0, tfAdmin, "api_response_time", 500m, "GreaterThan", "Current", null);
        R(t0, tfAdmin, "active_users", 800m, "LessThan", "Current", null);
        R(t0, tfAdmin, "churn_rate", 0.05m, "GreaterThan", "Current", null);
        R(t0, tfAdmin, "monthly_revenue", 70000m, "LessThan", "Predictive", 7);
        R(t0, tfAdmin, "customer_satisfaction", 3.5m, "LessThan", "Predictive", 14);

        R(t1, mhAdmin, "er_wait_time", 90m, "GreaterThan", "Current", null);
        R(t1, mhAdmin, "bed_occupancy", 0.95m, "GreaterThan", "Current", null);
        R(t1, mhAdmin, "staffing_ratio", 0.70m, "LessThan", "Current", null);
        R(t1, mhAdmin, "readmission_rate", 0.15m, "GreaterThan", "Predictive", 7);

        R(t2, ucAdmin, "page_load_time", 5.0m, "GreaterThan", "Current", null);
        R(t2, ucAdmin, "payment_success_rate", 0.95m, "LessThan", "Current", null);
        R(t2, ucAdmin, "checkout_conversion", 0.02m, "LessThan", "Current", null);
        R(t2, ucAdmin, "daily_revenue", 80000m, "LessThan", "Predictive", 7);

        context.AlertRules.AddRange(rules);
        await context.SaveChangesAsync();

        var tfRules = await context.AlertRules.IgnoreQueryFilters().Where(r => r.TenantId == t0).ToListAsync();
        var mhRules = await context.AlertRules.IgnoreQueryFilters().Where(r => r.TenantId == t1).ToListAsync();
        var ucRules = await context.AlertRules.IgnoreQueryFilters().Where(r => r.TenantId == t2).ToListAsync();

        AlertRule ByMetric(List<AlertRule> rs, string m) => rs.First(r => r.MetricName == m);

        var alerts = new List<Alert>
        {
            A(t0, ByMetric(tfRules, "error_rate").Id, 0.062m, false, null, DateTime.UtcNow.AddDays(-10), tfAdmin),
            A(t0, ByMetric(tfRules, "api_response_time").Id, 620m, false, null, DateTime.UtcNow.AddDays(-7), null),
            A(t0, ByMetric(tfRules, "active_users").Id, 760m, false, null, DateTime.UtcNow.AddDays(-4), tfAdmin),
            A(t0, ByMetric(tfRules, "churn_rate").Id, 0.058m, false, null, DateTime.UtcNow.AddDays(-2), null),
            A(t1, ByMetric(mhRules, "er_wait_time").Id, 102m, false, null, DateTime.UtcNow.AddDays(-9), mhAdmin),
            A(t1, ByMetric(mhRules, "bed_occupancy").Id, 0.97m, false, null, DateTime.UtcNow.AddDays(-5), null),
            A(t1, ByMetric(mhRules, "staffing_ratio").Id, 0.62m, false, null, DateTime.UtcNow.AddDays(-3), mhAdmin),
            A(t2, ByMetric(ucRules, "page_load_time").Id, 6.2m, false, null, DateTime.UtcNow.AddDays(-8), ucAdmin),
            A(t2, ByMetric(ucRules, "payment_success_rate").Id, 0.932m, false, null, DateTime.UtcNow.AddDays(-6), null),
            A(t2, ByMetric(ucRules, "checkout_conversion").Id, 0.017m, false, null, DateTime.UtcNow.AddDays(-4), ucAdmin),
            A(t2, ByMetric(ucRules, "daily_revenue").Id, 72000m, true, 78500m, DateTime.UtcNow.AddDays(-1), null),
        };

        context.Alerts.AddRange(alerts);
        await context.SaveChangesAsync();

        static Alert A(int tenantId, int ruleId, decimal value, bool pred, decimal? forecast, DateTime when, int? ackBy) =>
            new()
            {
                TenantId = tenantId,
                AlertRuleId = ruleId,
                MetricValue = value,
                IsPredictive = pred,
                ForecastedValue = forecast,
                TriggeredAt = when,
                AcknowledgedBy = ackBy,
                AcknowledgedAt = ackBy is not null ? when.AddHours(2) : null,
            };
    }

    private static async Task SeedHealthScoresAsync(AppDbContext context, int[] tenantIds, DateTime utcNow)
    {
        const int metricCount = 8;
        var rng = new Random(42);
        var allAnomalies = await context.AnomalyScores.IgnoreQueryFilters()
            .Where(a => tenantIds.Contains(a.TenantId))
            .ToListAsync();

        var scores = new List<HealthScore>();
        foreach (var tid in tenantIds)
        {
            var tenantAnomalies = allAnomalies.Where(a => a.TenantId == tid).ToList();
            for (var d = 29; d >= 0; d--)
            {
                var dayDate = utcNow.Date.AddDays(-d);
                var active = tenantAnomalies.Count(
                    a => a.DetectedAt.Date <= dayDate
                        && (a.ResolvedAt is null || a.ResolvedAt.Value.Date > dayDate));
                var normalPct = Math.Clamp(100m * (metricCount - Math.Min(active, metricCount)) / metricCount, 55m, 100m);
                var overall = Math.Clamp(88m - active * 7m + (decimal)(rng.NextDouble() * 5 - 2), 65m, 95m);
                var prevDayActive = tenantAnomalies.Count(
                    a => a.DetectedAt.Date <= dayDate.AddDays(-1)
                        && (a.ResolvedAt is null || a.ResolvedAt.Value.Date > dayDate.AddDays(-1)));
                var trendDelta = active - prevDayActive;
                var trendScore = Math.Clamp(78m - trendDelta * 4m + (decimal)rng.NextDouble() * 8m, 60m, 98m);
                var response = 60m + rng.Next(0, 36);
                scores.Add(
                    new HealthScore
                    {
                        TenantId = tid,
                        OverallScore = decimal.Round(overall, 2),
                        NormalMetricPct = decimal.Round(normalPct, 2),
                        ActiveAnomalies = active,
                        TrendScore = decimal.Round(trendScore, 2),
                        ResponseScore = decimal.Round(response, 2),
                        CalculatedAt = dayDate.AddHours(23),
                    });
            }
        }

        context.HealthScores.AddRange(scores);
        await context.SaveChangesAsync();
    }
}
