using Microsoft.AspNetCore.Authorization;
using UserAPI.Controllers;
using Xunit;

namespace UserAPI.Tests.Controllers;

public class ManagerViewSystemStatisticsUtcidTests
{
    [Fact(DisplayName="FUNC155-MGR-UTCID01")] public async Task Current_period_returns_candidate_total_and_manager_is_authorized(){AssertManagerRole();await StatsTest.Success(new DateOnly(2026,8,1),new DateOnly(2026,8,29),42);}
    [Fact(DisplayName="FUNC155-MGR-UTCID02")] public Task Single_day_statistics_are_supported()=>StatsTest.Success(new DateOnly(2026,8,29),new DateOnly(2026,8,29),1);
    [Fact(DisplayName="FUNC155-MGR-UTCID03")] public Task Empty_statistics_return_zero()=>StatsTest.Success(new DateOnly(2026,1,1),new DateOnly(2026,1,31),0);
    [Fact(DisplayName="FUNC155-MGR-UTCID04")] public Task Missing_start_date_is_rejected()=>StatsTest.Bad(null,new DateOnly(2026,1,1));
    [Fact(DisplayName="FUNC155-MGR-UTCID05")] public Task Missing_end_date_is_rejected()=>StatsTest.Bad(new DateOnly(2026,1,1),null);
    [Fact(DisplayName="FUNC155-MGR-UTCID06")] public Task Downstream_failure_maps_to_bad_gateway()=>StatsTest.Dependency();
    private static void AssertManagerRole(){var attribute=Assert.Single(typeof(ManagementUserStatisticsController).GetCustomAttributes(typeof(AuthorizeAttribute),true).Cast<AuthorizeAttribute>());Assert.Contains("Manager",attribute.Roles!.Split(','));}
}

public class ManagerFilterSystemStatisticsUtcidTests
{
    [Fact(DisplayName="FUNC156-MGR-UTCID01")] public Task Custom_date_range_is_forwarded()=>StatsTest.Success(new DateOnly(2026,2,1),new DateOnly(2026,2,28),28);
    [Fact(DisplayName="FUNC156-MGR-UTCID02")] public Task Reversed_date_range_is_rejected()=>StatsTest.Bad(new DateOnly(2026,2,2),new DateOnly(2026,2,1));
    [Fact(DisplayName="FUNC156-MGR-UTCID03")] public Task Leap_year_366_day_boundary_is_accepted()=>StatsTest.Success(new DateOnly(2024,1,1),new DateOnly(2024,12,31),366);
    [Fact(DisplayName="FUNC156-MGR-UTCID04")] public Task Range_over_366_days_is_rejected()=>StatsTest.Bad(new DateOnly(2024,1,1),new DateOnly(2025,1,1));
    [Fact(DisplayName="FUNC156-MGR-UTCID05")] public Task Manager_bearer_header_is_forwarded()=>StatsTest.Authorization();
    [Fact(DisplayName="FUNC156-MGR-UTCID06")] public Task Request_cancellation_token_is_forwarded()=>StatsTest.Cancellation();
}
