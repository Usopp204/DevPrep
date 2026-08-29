using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using QuestionAPI.Controllers;
using QuestionAPI.DTOs;
using QuestionAPI.Services;
using Xunit;
using static QuestionAPI.Tests.Controllers.ManagerQuestionTestSupport;

namespace QuestionAPI.Tests.Controllers;

public class ManagerQuestionBankUtcidTests
{
 [Fact(DisplayName="FUNC157-MGR-UTCID01")] public async Task List_returns_service_page(){var(c,s)=New();var page=Page(Item("Q1"));s.Setup(x=>x.GetListAsync(1,10,null,null,null,null,null,null)).ReturnsAsync(page);Assert.Same(page,Data<PagedResult<QuestionListItemDto>>(await c.GetList()));}
 [Fact(DisplayName="FUNC157-MGR-UTCID02")] public async Task List_preserves_empty_page(){var(c,s)=New();s.Setup(x=>x.GetListAsync(1,10,null,null,null,null,null,null)).ReturnsAsync(Page());Assert.Empty(Data<PagedResult<QuestionListItemDto>>(await c.GetList()).Items);}
 [Fact(DisplayName="FUNC157-MGR-UTCID03")] public async Task List_supports_second_page(){var(c,s)=New();var page=Page(Item("Q2"));page.Page=2;page.PageSize=20;s.Setup(x=>x.GetListAsync(2,20,null,null,null,null,null,null)).ReturnsAsync(page);Assert.Equal(2,Data<PagedResult<QuestionListItemDto>>(await c.GetList(2,20)).Page);}
 [Fact(DisplayName="FUNC157-MGR-UTCID04")] public async Task List_normalizes_nonpositive_page(){var(c,s)=New();s.Setup(x=>x.GetListAsync(1,10,null,null,null,null,null,null)).ReturnsAsync(Page());_ =await c.GetList(-1,10);s.VerifyAll();}
 [Fact(DisplayName="FUNC157-MGR-UTCID05")] public async Task List_normalizes_sizes_outside_one_to_one_hundred(){var(c,s)=New();s.Setup(x=>x.GetListAsync(1,10,null,null,null,null,null,null)).ReturnsAsync(Page());_ =await c.GetList(1,0);_ =await c.GetList(1,101);s.Verify(x=>x.GetListAsync(1,10,null,null,null,null,null,null),Times.Exactly(2));}
 [Fact(DisplayName="FUNC157-MGR-UTCID06")] public async Task List_endpoint_authorizes_manager(){var roles=Assert.Single(typeof(ManagerQuestionsController).GetCustomAttributes(typeof(AuthorizeAttribute),true).Cast<AuthorizeAttribute>()).Roles!;Assert.Contains("Manager",roles.Split(','));await Task.CompletedTask;}
}

public class ManagerQuestionDetailUtcidTests
{
 [Fact(DisplayName="FUNC158-MGR-UTCID01")] public async Task Detail_returns_reference_answer(){var(c,s)=New();var item=Detail("Answer");s.Setup(x=>x.GetByIdAsync(item.QuestionId)).ReturnsAsync(item);Assert.Equal("Answer",Data<QuestionDetailDto>(await c.GetById(item.QuestionId)).Answer);}
 [Fact(DisplayName="FUNC158-MGR-UTCID02")] public async Task Detail_maps_all_classification_fields(){var(c,s)=New();var item=Detail("A");item.Category="Backend";item.Difficulty="Hard";item.Position="Senior";s.Setup(x=>x.GetByIdAsync(item.QuestionId)).ReturnsAsync(item);var data=Data<QuestionDetailDto>(await c.GetById(item.QuestionId));Assert.Equal(("Backend","Hard","Senior"),(data.Category,data.Difficulty,data.Position));}
 [Fact(DisplayName="FUNC158-MGR-UTCID03")] public async Task Detail_returns_deleted_state_for_manager(){var(c,s)=New();var item=Detail(null);item.IsDeleted=true;item.DeletedAt=DateTime.UtcNow;s.Setup(x=>x.GetByIdAsync(item.QuestionId)).ReturnsAsync(item);Assert.True(Data<QuestionDetailDto>(await c.GetById(item.QuestionId)).IsDeleted);}
 [Fact(DisplayName="FUNC158-MGR-UTCID04")] public async Task Detail_unknown_id_returns_not_found(){var(c,s)=New();var id=Guid.NewGuid();s.Setup(x=>x.GetByIdAsync(id)).ReturnsAsync((QuestionDetailDto?)null);var result=Assert.IsType<NotFoundObjectResult>(await c.GetById(id));Assert.Contains(id.ToString(),Assert.IsType<ApiResponse<QuestionDetailDto>>(result.Value).Message);}
 [Fact(DisplayName="FUNC158-MGR-UTCID05")] public async Task Detail_empty_guid_boundary_is_forwarded(){var(c,s)=New();s.Setup(x=>x.GetByIdAsync(Guid.Empty)).ReturnsAsync((QuestionDetailDto?)null);Assert.IsType<NotFoundObjectResult>(await c.GetById(Guid.Empty));s.VerifyAll();}
 [Fact(DisplayName="FUNC158-MGR-UTCID06")] public async Task Detail_propagates_dependency_failure(){var(c,s)=New();s.Setup(x=>x.GetByIdAsync(It.IsAny<Guid>())).ThrowsAsync(new TimeoutException());await Assert.ThrowsAsync<TimeoutException>(()=>c.GetById(Guid.NewGuid()));}
}

public class ManagerSearchQuestionUtcidTests
{
 [Fact(DisplayName="FUNC159-MGR-UTCID01")] public Task Search_forwards_keyword()=>Search("dependency injection");
 [Fact(DisplayName="FUNC159-MGR-UTCID02")] public Task Search_preserves_whitespace_for_service_normalization()=>Search("  backend  ");
 [Fact(DisplayName="FUNC159-MGR-UTCID03")] public Task Search_supports_unicode()=>Search("phỏng vấn");
 [Fact(DisplayName="FUNC159-MGR-UTCID04")] public Task Search_supports_empty_keyword()=>Search("");
 [Fact(DisplayName="FUNC159-MGR-UTCID05")] public Task Search_supports_long_boundary_keyword()=>Search(new string('x',200));
 [Fact(DisplayName="FUNC159-MGR-UTCID06")] public async Task Search_propagates_service_failure(){var(c,s)=New();s.Setup(x=>x.GetListAsync(1,10,"fail",null,null,null,null,null)).ThrowsAsync(new InvalidOperationException());await Assert.ThrowsAsync<InvalidOperationException>(()=>c.GetList(keyword:"fail"));}
 private static async Task Search(string keyword){var(c,s)=New();s.Setup(x=>x.GetListAsync(1,10,keyword,null,null,null,null,null)).ReturnsAsync(Page(Item(keyword)));var data=Data<PagedResult<QuestionListItemDto>>(await c.GetList(keyword:keyword));Assert.Single(data.Items);s.VerifyAll();}
}

public class ManagerFilterQuestionUtcidTests
{
 [Fact(DisplayName="FUNC160-MGR-UTCID01")] public Task Filter_by_question_type()=>Filter("Technical",null,null,null,null);
 [Fact(DisplayName="FUNC160-MGR-UTCID02")] public Task Filter_by_level_and_difficulty()=>Filter(null,"Senior","Hard",null,null);
 [Fact(DisplayName="FUNC160-MGR-UTCID03")] public Task Filter_by_status()=>Filter(null,null,null,"Active",null);
 [Fact(DisplayName="FUNC160-MGR-UTCID04")] public Task Filter_by_category()=>Filter(null,null,null,null,Guid.NewGuid());
 [Fact(DisplayName="FUNC160-MGR-UTCID05")] public Task Filter_empty_guid_category_boundary()=>Filter(null,null,null,null,Guid.Empty);
 [Fact(DisplayName="FUNC160-MGR-UTCID06")] public async Task Combined_filters_are_forwarded_together(){var(c,s)=New();var id=Guid.NewGuid();s.Setup(x=>x.GetListAsync(3,50,"api","Technical","Middle","Medium","Deleted",id)).ReturnsAsync(Page());_ =await c.GetList(3,50,"api","Technical","Middle","Medium","Deleted",id);s.VerifyAll();}
 private static async Task Filter(string? type,string? level,string? difficulty,string? status,Guid? category){var(c,s)=New();s.Setup(x=>x.GetListAsync(1,10,null,type,level,difficulty,status,category)).ReturnsAsync(Page());_ =await c.GetList(questionType:type,level:level,difficulty:difficulty,status:status,categoryId:category);s.VerifyAll();}
}

file static class ManagerQuestionTestSupport
{
 public static (ManagerQuestionsController,Mock<IQuestionService>) New(){var service=new Mock<IQuestionService>();return(new ManagerQuestionsController(service.Object),service);}
 public static PagedResult<QuestionListItemDto> Page(params QuestionListItemDto[] items)=>new(){Items=[..items],TotalCount=items.Length,Page=1,PageSize=10};
 public static QuestionListItemDto Item(string question)=>new(){QuestionId=Guid.NewGuid(),Question=question,Status="Active",Difficulty="Medium"};
 public static QuestionDetailDto Detail(string? answer)=>new(){QuestionId=Guid.NewGuid(),Question="Question",Answer=answer,Status="Active",Difficulty="Medium"};
 public static T Data<T>(IActionResult result)=>Assert.IsType<ApiResponse<T>>(Assert.IsType<OkObjectResult>(result).Value).Data!;
}
