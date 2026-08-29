using Microsoft.AspNetCore.Mvc;
using Moq;
using QuestionAPI.Controllers;
using QuestionAPI.DTOs;
using QuestionAPI.DTOs.Positions;
using QuestionAPI.DTOs.TechStacks;
using QuestionAPI.Exceptions;
using QuestionAPI.Services.Interfaces;
using Xunit;

namespace QuestionAPI.Tests.Controllers;

public class ManagerCatalogUtcidTests
{
    [Fact(DisplayName="FUNC184-MGR-UTCID01")] public async Task Tech_list_returns_page(){var(c,s)=Tech();var q=new TechStackQueryDto();var p=TechPage(".NET");s.Setup(x=>x.GetPageAsync(q,default)).ReturnsAsync(p);Assert.Same(p,Data<TechStackPagedResultDto>(await c.Get(q,default)));}
    [Fact(DisplayName="FUNC184-MGR-UTCID02")] public async Task Tech_list_forwards_pagination(){var(c,s)=Tech();var q=new TechStackQueryDto{PageIndex=2,PageSize=25};s.Setup(x=>x.GetPageAsync(q,default)).ReturnsAsync(new TechStackPagedResultDto());await c.Get(q,default);s.Verify(x=>x.GetPageAsync(It.Is<TechStackQueryDto>(v=>v.PageIndex==2&&v.PageSize==25),default));}
    [Fact(DisplayName="FUNC184-MGR-UTCID03")] public async Task Tech_list_can_be_empty(){var(c,s)=Tech();var q=new TechStackQueryDto();s.Setup(x=>x.GetPageAsync(q,default)).ReturnsAsync(new TechStackPagedResultDto{Items=[]});Assert.Empty(Data<TechStackPagedResultDto>(await c.Get(q,default)).Items);}
    [Fact(DisplayName="FUNC184-MGR-UTCID04")] public async Task Tech_list_maps_invalid_page(){var(c,s)=Tech();var q=new TechStackQueryDto{PageIndex=0};s.Setup(x=>x.GetPageAsync(q,default)).ThrowsAsync(new TechStackValidationException("invalid page"));Assert.Equal("invalid page",Error(await c.Get(q,default)));}
    [Fact(DisplayName="FUNC184-MGR-UTCID05")] public async Task Tech_list_accepts_max_page_size(){var(c,s)=Tech();var q=new TechStackQueryDto{PageSize=100};s.Setup(x=>x.GetPageAsync(q,default)).ReturnsAsync(new TechStackPagedResultDto());Assert.IsType<OkObjectResult>(await c.Get(q,default));}
    [Fact(DisplayName="FUNC184-MGR-UTCID06")] public async Task Tech_list_forwards_cancellation(){var(c,s)=Tech();var q=new TechStackQueryDto();using var cs=new CancellationTokenSource();s.Setup(x=>x.GetPageAsync(q,cs.Token)).ReturnsAsync(new TechStackPagedResultDto());await c.Get(q,cs.Token);s.Verify(x=>x.GetPageAsync(q,cs.Token));}

    [Fact(DisplayName="FUNC185-MGR-UTCID01")] public async Task Tech_search_returns_match(){var(c,s)=Tech();var q=new TechStackQueryDto{Search="dotnet"};s.Setup(x=>x.GetPageAsync(q,default)).ReturnsAsync(TechPage(".NET"));Assert.Equal(".NET",Data<TechStackPagedResultDto>(await c.Get(q,default)).Items.Single().Name);}
    [Fact(DisplayName="FUNC185-MGR-UTCID02")] public async Task Tech_search_forwards_term_verbatim(){var(c,s)=Tech();var q=new TechStackQueryDto{Search="  java  "};s.Setup(x=>x.GetPageAsync(q,default)).ReturnsAsync(new TechStackPagedResultDto());await c.Get(q,default);s.Verify(x=>x.GetPageAsync(It.Is<TechStackQueryDto>(v=>v.Search=="  java  "),default));}
    [Fact(DisplayName="FUNC185-MGR-UTCID03")] public async Task Tech_search_no_match_is_empty(){var(c,s)=Tech();var q=new TechStackQueryDto{Search="missing"};s.Setup(x=>x.GetPageAsync(q,default)).ReturnsAsync(new TechStackPagedResultDto{Items=[]});Assert.Empty(Data<TechStackPagedResultDto>(await c.Get(q,default)).Items);}
    [Fact(DisplayName="FUNC185-MGR-UTCID04")] public async Task Tech_search_maps_validation_error(){var(c,s)=Tech();var q=new TechStackQueryDto{Search=new string('x',1000)};s.Setup(x=>x.GetPageAsync(q,default)).ThrowsAsync(new TechStackValidationException("invalid search"));Assert.IsType<BadRequestObjectResult>(await c.Get(q,default));}
    [Fact(DisplayName="FUNC185-MGR-UTCID05")] public async Task Tech_search_accepts_empty_term(){var(c,s)=Tech();var q=new TechStackQueryDto{Search=""};s.Setup(x=>x.GetPageAsync(q,default)).ReturnsAsync(new TechStackPagedResultDto());Assert.IsType<OkObjectResult>(await c.Get(q,default));}
    [Fact(DisplayName="FUNC185-MGR-UTCID06")] public async Task Tech_search_preserves_result_metadata(){var(c,s)=Tech();var q=new TechStackQueryDto{Search="net"};var p=TechPage(".NET");p.TotalRecords=7;p.TotalPages=2;s.Setup(x=>x.GetPageAsync(q,default)).ReturnsAsync(p);Assert.Equal((7,2),(Data<TechStackPagedResultDto>(await c.Get(q,default)).TotalRecords,Data<TechStackPagedResultDto>(await c.Get(q,default)).TotalPages));}

    [Theory(DisplayName="FUNC186-MGR-UTCID01")][InlineData(TechStackStatusFilter.All)] public async Task Tech_filter_all(TechStackStatusFilter status)=>await AssertTechFilter(status);
    [Theory(DisplayName="FUNC186-MGR-UTCID02")][InlineData(TechStackStatusFilter.Active)] public async Task Tech_filter_active(TechStackStatusFilter status)=>await AssertTechFilter(status);
    [Theory(DisplayName="FUNC186-MGR-UTCID03")][InlineData(TechStackStatusFilter.Inactive)] public async Task Tech_filter_inactive(TechStackStatusFilter status)=>await AssertTechFilter(status);
    [Fact(DisplayName="FUNC186-MGR-UTCID04")] public async Task Tech_filter_maps_invalid_value(){var(c,s)=Tech();var q=new TechStackQueryDto{Status=(TechStackStatusFilter)999};s.Setup(x=>x.GetPageAsync(q,default)).ThrowsAsync(new TechStackValidationException("invalid status"));Assert.IsType<BadRequestObjectResult>(await c.Get(q,default));}
    [Fact(DisplayName="FUNC186-MGR-UTCID05")] public async Task Tech_filter_combines_search_and_boundary_page(){var(c,s)=Tech();var q=new TechStackQueryDto{Status=TechStackStatusFilter.Active,Search="net",PageSize=100};s.Setup(x=>x.GetPageAsync(q,default)).ReturnsAsync(new TechStackPagedResultDto());await c.Get(q,default);s.Verify(x=>x.GetPageAsync(q,default));}
    [Fact(DisplayName="FUNC186-MGR-UTCID06")] public async Task Tech_filter_returns_service_selection(){var(c,s)=Tech();var q=new TechStackQueryDto{Status=TechStackStatusFilter.Inactive};var p=TechPage("Legacy");s.Setup(x=>x.GetPageAsync(q,default)).ReturnsAsync(p);Assert.Equal("Legacy",Data<TechStackPagedResultDto>(await c.Get(q,default)).Items.Single().Name);}

    [Fact(DisplayName="FUNC187-MGR-UTCID01")] public async Task Position_list_returns_page(){var(c,s)=Position();var q=new PositionQueryDto();var p=PositionPage("Backend");s.Setup(x=>x.GetPageAsync(q,default)).ReturnsAsync(p);Assert.Same(p,Data<PositionPagedResultDto>(await c.Get(q,default)));}
    [Fact(DisplayName="FUNC187-MGR-UTCID02")] public async Task Position_list_forwards_pagination(){var(c,s)=Position();var q=new PositionQueryDto{PageIndex=2,PageSize=25};s.Setup(x=>x.GetPageAsync(q,default)).ReturnsAsync(new PositionPagedResultDto());await c.Get(q,default);s.Verify(x=>x.GetPageAsync(q,default));}
    [Fact(DisplayName="FUNC187-MGR-UTCID03")] public async Task Position_list_can_be_empty(){var(c,s)=Position();var q=new PositionQueryDto();s.Setup(x=>x.GetPageAsync(q,default)).ReturnsAsync(new PositionPagedResultDto{Items=[]});Assert.Empty(Data<PositionPagedResultDto>(await c.Get(q,default)).Items);}
    [Fact(DisplayName="FUNC187-MGR-UTCID04")] public async Task Position_list_maps_invalid_page(){var(c,s)=Position();var q=new PositionQueryDto{PageIndex=0};s.Setup(x=>x.GetPageAsync(q,default)).ThrowsAsync(new PositionValidationException("invalid page"));Assert.Equal("invalid page",Error(await c.Get(q,default)));}
    [Fact(DisplayName="FUNC187-MGR-UTCID05")] public async Task Position_list_accepts_max_page_size(){var(c,s)=Position();var q=new PositionQueryDto{PageSize=100};s.Setup(x=>x.GetPageAsync(q,default)).ReturnsAsync(new PositionPagedResultDto());Assert.IsType<OkObjectResult>(await c.Get(q,default));}
    [Fact(DisplayName="FUNC187-MGR-UTCID06")] public async Task Position_list_forwards_cancellation(){var(c,s)=Position();var q=new PositionQueryDto();using var cs=new CancellationTokenSource();s.Setup(x=>x.GetPageAsync(q,cs.Token)).ReturnsAsync(new PositionPagedResultDto());await c.Get(q,cs.Token);s.Verify(x=>x.GetPageAsync(q,cs.Token));}

    [Fact(DisplayName="FUNC188-MGR-UTCID01")] public async Task Position_search_returns_match(){var(c,s)=Position();var q=new PositionQueryDto{Search="back"};s.Setup(x=>x.GetPageAsync(q,default)).ReturnsAsync(PositionPage("Backend"));Assert.Equal("Backend",Data<PositionPagedResultDto>(await c.Get(q,default)).Items.Single().Name);}
    [Fact(DisplayName="FUNC188-MGR-UTCID02")] public async Task Position_search_forwards_term_verbatim(){var(c,s)=Position();var q=new PositionQueryDto{Search="  front  "};s.Setup(x=>x.GetPageAsync(q,default)).ReturnsAsync(new PositionPagedResultDto());await c.Get(q,default);s.Verify(x=>x.GetPageAsync(q,default));}
    [Fact(DisplayName="FUNC188-MGR-UTCID03")] public async Task Position_search_no_match_is_empty(){var(c,s)=Position();var q=new PositionQueryDto{Search="missing"};s.Setup(x=>x.GetPageAsync(q,default)).ReturnsAsync(new PositionPagedResultDto{Items=[]});Assert.Empty(Data<PositionPagedResultDto>(await c.Get(q,default)).Items);}
    [Fact(DisplayName="FUNC188-MGR-UTCID04")] public async Task Position_search_maps_validation_error(){var(c,s)=Position();var q=new PositionQueryDto{Search=new string('x',1000)};s.Setup(x=>x.GetPageAsync(q,default)).ThrowsAsync(new PositionValidationException("invalid search"));Assert.IsType<BadRequestObjectResult>(await c.Get(q,default));}
    [Fact(DisplayName="FUNC188-MGR-UTCID05")] public async Task Position_search_accepts_empty_term(){var(c,s)=Position();var q=new PositionQueryDto{Search=""};s.Setup(x=>x.GetPageAsync(q,default)).ReturnsAsync(new PositionPagedResultDto());Assert.IsType<OkObjectResult>(await c.Get(q,default));}
    [Fact(DisplayName="FUNC188-MGR-UTCID06")] public async Task Position_search_preserves_metadata(){var(c,s)=Position();var q=new PositionQueryDto{Search="end"};var p=PositionPage("Backend");p.TotalRecords=3;s.Setup(x=>x.GetPageAsync(q,default)).ReturnsAsync(p);Assert.Equal(3,Data<PositionPagedResultDto>(await c.Get(q,default)).TotalRecords);}

    private static async Task AssertTechFilter(TechStackStatusFilter status){var(c,s)=Tech();var q=new TechStackQueryDto{Status=status};s.Setup(x=>x.GetPageAsync(q,default)).ReturnsAsync(new TechStackPagedResultDto());await c.Get(q,default);s.Verify(x=>x.GetPageAsync(It.Is<TechStackQueryDto>(v=>v.Status==status),default));}
    private static (ManagerTechStacksController,Mock<ITechStackService>) Tech(){var s=new Mock<ITechStackService>();return(new(s.Object),s);}
    private static (ManagerPositionsController,Mock<IPositionService>) Position(){var s=new Mock<IPositionService>();return(new(s.Object),s);}
    private static TechStackPagedResultDto TechPage(string name)=>new(){Items=[new TechStackListItemDto{Name=name}],TotalRecords=1};
    private static PositionPagedResultDto PositionPage(string name)=>new(){Items=[new PositionDto{Name=name}],TotalRecords=1};
    private static T Data<T>(IActionResult result)=>Assert.IsType<ApiResponse<T>>(Assert.IsType<OkObjectResult>(result).Value).Data!;
    private static string Error(IActionResult result)=>Assert.IsType<ApiResponse<object>>(Assert.IsType<BadRequestObjectResult>(result).Value).Message!;
}
