using Moq;
using Xunit;

namespace FeedbackAPI.Tests;

public class Func69UtcidTests
{
    [Fact(DisplayName="FUNC069-USR-UTCID01")] public Task Case1() => CandidateFeedbackUtcid.Run(69, 1);
    [Fact(DisplayName="FUNC069-USR-UTCID02")] public Task Case2() => CandidateFeedbackUtcid.Run(69, 2);
    [Fact(DisplayName="FUNC069-USR-UTCID03")] public Task Case3() => CandidateFeedbackUtcid.Run(69, 3);
    [Fact(DisplayName="FUNC069-USR-UTCID04")] public Task Case4() => CandidateFeedbackUtcid.Run(69, 4);
    [Fact(DisplayName="FUNC069-USR-UTCID05")] public Task Case5() => CandidateFeedbackUtcid.Run(69, 5);
    [Fact(DisplayName="FUNC069-USR-UTCID06")] public Task Case6() => CandidateFeedbackUtcid.Run(69, 6);
}

public class Func70UtcidTests
{
    [Fact(DisplayName="FUNC070-USR-UTCID01")] public Task Case1() => CandidateFeedbackUtcid.Run(70, 1);
    [Fact(DisplayName="FUNC070-USR-UTCID02")] public Task Case2() => CandidateFeedbackUtcid.Run(70, 2);
    [Fact(DisplayName="FUNC070-USR-UTCID03")] public Task Case3() => CandidateFeedbackUtcid.Run(70, 3);
    [Fact(DisplayName="FUNC070-USR-UTCID04")] public Task Case4() => CandidateFeedbackUtcid.Run(70, 4);
    [Fact(DisplayName="FUNC070-USR-UTCID05")] public Task Case5() => CandidateFeedbackUtcid.Run(70, 5);
    [Fact(DisplayName="FUNC070-USR-UTCID06")] public Task Case6() => CandidateFeedbackUtcid.Run(70, 6);
}

public class Func71UtcidTests
{
    [Fact(DisplayName="FUNC071-USR-UTCID01")] public Task Case1() => CandidateFeedbackUtcid.Run(71, 1);
    [Fact(DisplayName="FUNC071-USR-UTCID02")] public Task Case2() => CandidateFeedbackUtcid.Run(71, 2);
    [Fact(DisplayName="FUNC071-USR-UTCID03")] public Task Case3() => CandidateFeedbackUtcid.Run(71, 3);
    [Fact(DisplayName="FUNC071-USR-UTCID04")] public Task Case4() => CandidateFeedbackUtcid.Run(71, 4);
    [Fact(DisplayName="FUNC071-USR-UTCID05")] public Task Case5() => CandidateFeedbackUtcid.Run(71, 5);
    [Fact(DisplayName="FUNC071-USR-UTCID06")] public Task Case6() => CandidateFeedbackUtcid.Run(71, 6);
}

public class Func72UtcidTests
{
    [Fact(DisplayName="FUNC072-USR-UTCID01")] public Task Case1() => CandidateFeedbackUtcid.Run(72, 1);
    [Fact(DisplayName="FUNC072-USR-UTCID02")] public Task Case2() => CandidateFeedbackUtcid.Run(72, 2);
    [Fact(DisplayName="FUNC072-USR-UTCID03")] public Task Case3() => CandidateFeedbackUtcid.Run(72, 3);
    [Fact(DisplayName="FUNC072-USR-UTCID04")] public Task Case4() => CandidateFeedbackUtcid.Run(72, 4);
    [Fact(DisplayName="FUNC072-USR-UTCID05")] public Task Case5() => CandidateFeedbackUtcid.Run(72, 5);
    [Fact(DisplayName="FUNC072-USR-UTCID06")] public Task Case6() => CandidateFeedbackUtcid.Run(72, 6);
}

public class Func73UtcidTests
{
    [Fact(DisplayName="FUNC073-USR-UTCID01")] public Task Case1() => CandidateFeedbackUtcid.Run(73, 1);
    [Fact(DisplayName="FUNC073-USR-UTCID02")] public Task Case2() => CandidateFeedbackUtcid.Run(73, 2);
    [Fact(DisplayName="FUNC073-USR-UTCID03")] public Task Case3() => CandidateFeedbackUtcid.Run(73, 3);
    [Fact(DisplayName="FUNC073-USR-UTCID04")] public Task Case4() => CandidateFeedbackUtcid.Run(73, 4);
    [Fact(DisplayName="FUNC073-USR-UTCID05")] public Task Case5() => CandidateFeedbackUtcid.Run(73, 5);
    [Fact(DisplayName="FUNC073-USR-UTCID06")] public Task Case6() => CandidateFeedbackUtcid.Run(73, 6);
}

public class Func74UtcidTests
{
    [Fact(DisplayName="FUNC074-USR-UTCID01")] public Task Case1() => CandidateFeedbackUtcid.Run(74, 1);
    [Fact(DisplayName="FUNC074-USR-UTCID02")] public Task Case2() => CandidateFeedbackUtcid.Run(74, 2);
    [Fact(DisplayName="FUNC074-USR-UTCID03")] public Task Case3() => CandidateFeedbackUtcid.Run(74, 3);
    [Fact(DisplayName="FUNC074-USR-UTCID04")] public Task Case4() => CandidateFeedbackUtcid.Run(74, 4);
    [Fact(DisplayName="FUNC074-USR-UTCID05")] public Task Case5() => CandidateFeedbackUtcid.Run(74, 5);
    [Fact(DisplayName="FUNC074-USR-UTCID06")] public Task Case6() => CandidateFeedbackUtcid.Run(74, 6);
}

internal static class CandidateFeedbackUtcid
{
    internal static Task Run(int function, int scenario) => function switch
    {
        69 => ViewSystem(scenario), 70 => EditSystem(scenario), 71 => DeleteSystem(scenario),
        72 => ViewAi(scenario), 73 => EditAi(scenario), 74 => DeleteAi(scenario),
        _ => throw new ArgumentOutOfRangeException(nameof(function))
    };

    private static (FeedbackAPI.Controllers.FeedbackController C, Moq.Mock<FeedbackAPI.Services.Interfaces.ISystemFeedbackService> S, Moq.Mock<FeedbackAPI.Services.Interfaces.IAiFeedbackService> A, Moq.Mock<FeedbackAPI.Services.Interfaces.IFeedbackReportService> R, Guid U) Create(bool auth=true)
    {
        var s=new Moq.Mock<FeedbackAPI.Services.Interfaces.ISystemFeedbackService>();var a=new Moq.Mock<FeedbackAPI.Services.Interfaces.IAiFeedbackService>();var r=new Moq.Mock<FeedbackAPI.Services.Interfaces.IFeedbackReportService>();var u=Guid.NewGuid();var ctx=new Microsoft.AspNetCore.Http.DefaultHttpContext();
        if(auth)ctx.User=new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]{new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier,u.ToString()),new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role,"User")}));
        return(new FeedbackAPI.Controllers.FeedbackController(s.Object,a.Object,r.Object){ControllerContext=new Microsoft.AspNetCore.Mvc.ControllerContext{HttpContext=ctx}},s,a,r,u);
    }
    private static async Task ViewSystem(int x){var(c,s,_,r,u)=Create(x!=4);var q=new FeedbackAPI.DTOs.PublicFeedbackQuery{Page=x==3?int.MaxValue:1,PageSize=x==3?int.MaxValue:5};if(x==6){s.Setup(z=>z.GetPublicReviewsAsync(q,It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db"));await Assert.ThrowsAsync<InvalidOperationException>(()=>c.GetSystemReviews(q,default));return;}var items=x==2?Array.Empty<FeedbackAPI.DTOs.PublicSystemFeedbackDto>():new[]{new FeedbackAPI.DTOs.PublicSystemFeedbackDto{FeedbackId=Guid.NewGuid()}};s.Setup(z=>z.GetPublicReviewsAsync(q,It.IsAny<CancellationToken>())).ReturnsAsync(new FeedbackAPI.DTOs.PagedResult<FeedbackAPI.DTOs.PublicSystemFeedbackDto>{Items=items,Page=q.Page,PageSize=q.PageSize,TotalItems=items.Length});r.Setup(z=>z.ApplySystemViewerStateAsync(It.IsAny<IReadOnlyList<FeedbackAPI.DTOs.PublicSystemFeedbackDto>>(),x==4?null:u,x!=4,It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);var ok=Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(await c.GetSystemReviews(q,default));Assert.NotNull(ok.Value);}
    private static async Task ViewAi(int x){var(c,_,a,_,u)=Create(x!=4);var q=new FeedbackAPI.DTOs.PublicFeedbackQuery{Page=x==3?int.MaxValue:1,PageSize=x==3?int.MaxValue:5};if(x==6){a.Setup(z=>z.GetPublicReviewsAsync(q,It.IsAny<Guid?>(),It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db"));await Assert.ThrowsAsync<InvalidOperationException>(()=>c.GetAiReviews(q,default));return;}var items=x==2?Array.Empty<FeedbackAPI.DTOs.PublicAiFeedbackDto>():new[]{new FeedbackAPI.DTOs.PublicAiFeedbackDto{FeedbackId=Guid.NewGuid(),OverallRating=5}};a.Setup(z=>z.GetPublicReviewsAsync(q,x==4?null:u,It.IsAny<CancellationToken>())).ReturnsAsync(new FeedbackAPI.DTOs.PagedResult<FeedbackAPI.DTOs.PublicAiFeedbackDto>{Items=items,TotalItems=items.Length});Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(await c.GetAiReviews(q,default));}
    private static async Task EditSystem(int x){var(c,s,_,_,u)=Create(x!=2);var id=x==5?Guid.Empty:Guid.NewGuid();var req=new FeedbackAPI.DTOs.UpdateSystemFeedbackRequest{OverallRating=5,UiUxRating=5,UsabilityRating=5,FeatureQualityRating=5};if(x==3)s.Setup(z=>z.EditAsync(id,u,req,It.IsAny<CancellationToken>())).ThrowsAsync(new FeedbackAPI.Services.FeedbackValidationException("invalid"));else if(x==4)s.Setup(z=>z.EditAsync(id,u,req,It.IsAny<CancellationToken>())).ThrowsAsync(new FeedbackAPI.Services.FeedbackNotFoundException());else if(x==6)s.Setup(z=>z.EditAsync(id,u,req,It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db"));else if(x!=2)s.Setup(z=>z.EditAsync(id,u,req,It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);if(x==6){await Assert.ThrowsAsync<InvalidOperationException>(()=>c.EditSystem(id,req,default));return;}var result=await c.EditSystem(id,req,default);Assert.Equal(x==2?401:x==3?400:x==4?404:200,((Microsoft.AspNetCore.Mvc.ObjectResult)result).StatusCode);}
    private static async Task EditAi(int x){var(c,_,a,_,u)=Create(x!=2);var id=x==5?Guid.Empty:Guid.NewGuid();var req=new FeedbackAPI.DTOs.UpdateInterviewFeedbackRequest{OverallRating=5,QuestionQualityRating=5,AiAccuracyRating=5,ScoringFairnessRating=5,FeedbackUsefulnessRating=5};if(x==3)a.Setup(z=>z.EditAsync(id,u,req,It.IsAny<CancellationToken>())).ThrowsAsync(new FeedbackAPI.Services.FeedbackValidationException("invalid"));else if(x==4)a.Setup(z=>z.EditAsync(id,u,req,It.IsAny<CancellationToken>())).ThrowsAsync(new FeedbackAPI.Services.FeedbackNotFoundException());else if(x==6)a.Setup(z=>z.EditAsync(id,u,req,It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db"));else if(x!=2)a.Setup(z=>z.EditAsync(id,u,req,It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);if(x==6){await Assert.ThrowsAsync<InvalidOperationException>(()=>c.EditInterview(id,req,default));return;}var result=await c.EditInterview(id,req,default);Assert.Equal(x==2?401:x==3?400:x==4?404:200,((Microsoft.AspNetCore.Mvc.ObjectResult)result).StatusCode);}
    private static Task DeleteSystem(int x)=>Delete(x,false);
    private static Task DeleteAi(int x)=>Delete(x,true);
    private static async Task Delete(int x,bool ai){var(c,s,a,_,u)=Create(x!=2);var id=x==4?Guid.Empty:x==5?Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"):Guid.NewGuid();if(x==3){if(ai)a.Setup(z=>z.CandidateDeleteAsync(id,u,It.IsAny<CancellationToken>())).ThrowsAsync(new FeedbackAPI.Services.FeedbackNotFoundException());else s.Setup(z=>z.CandidateDeleteAsync(id,u,It.IsAny<CancellationToken>())).ThrowsAsync(new FeedbackAPI.Services.FeedbackNotFoundException());}else if(x==6){if(ai)a.Setup(z=>z.CandidateDeleteAsync(id,u,It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db"));else s.Setup(z=>z.CandidateDeleteAsync(id,u,It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db"));}else if(x!=2){if(ai)a.Setup(z=>z.CandidateDeleteAsync(id,u,It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);else s.Setup(z=>z.CandidateDeleteAsync(id,u,It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);}Func<Task<Microsoft.AspNetCore.Mvc.IActionResult>> action=()=>ai?c.DeleteInterview(id,default):c.DeleteSystem(id,default);if(x==6){await Assert.ThrowsAsync<InvalidOperationException>(action);return;}var result=await action();Assert.Equal(x==2?401:x==3?404:200,((Microsoft.AspNetCore.Mvc.ObjectResult)result).StatusCode);}
}
