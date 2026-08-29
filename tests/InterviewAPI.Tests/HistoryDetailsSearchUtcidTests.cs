using InterviewAPI.DTOs.Session;
using Moq;
using Xunit;

namespace InterviewAPI.Tests;

public class SessionHistoryUtcidTests
{
 [Fact(DisplayName="FUNC063-USR-UTCID01")] public async Task Returns_paged_history(){var(c,s,_,_,u)=MediaTest.Create();var q=new SessionHistoryQuery();s.Setup(x=>x.GetSessionHistoryAsync(u,q,It.IsAny<CancellationToken>())).ReturnsAsync(new SessionHistoryResult{TotalItems=2});Assert.Equal(200,MediaTest.Status(await c.GetSessionHistory(q,default)));}
 [Fact(DisplayName="FUNC063-USR-UTCID02")] public async Task Empty_history_is_valid(){var(c,s,_,_,_)=MediaTest.Create();s.Setup(x=>x.GetSessionHistoryAsync(It.IsAny<Guid>(),It.IsAny<SessionHistoryQuery>(),It.IsAny<CancellationToken>())).ReturnsAsync(new SessionHistoryResult());Assert.Equal(200,MediaTest.Status(await c.GetSessionHistory(new SessionHistoryQuery(),default)));}
 [Fact(DisplayName="FUNC063-USR-UTCID03")] public async Task Missing_identity_is_unauthorized(){var(c,s,_,_,_)=MediaTest.Create(false);Assert.Equal(401,MediaTest.Status(await c.GetSessionHistory(new SessionHistoryQuery(),default)));s.Verify(x=>x.GetSessionHistoryAsync(It.IsAny<Guid>(),It.IsAny<SessionHistoryQuery>(),It.IsAny<CancellationToken>()),Times.Never);}
 [Fact(DisplayName="FUNC063-USR-UTCID04")] public async Task Invalid_date_range_returns_bad_request(){var(c,s,_,_,_)=MediaTest.Create();s.Setup(x=>x.GetSessionHistoryAsync(It.IsAny<Guid>(),It.IsAny<SessionHistoryQuery>(),It.IsAny<CancellationToken>())).ThrowsAsync(new ArgumentException("dates"));Assert.Equal(400,MediaTest.Status(await c.GetSessionHistory(new SessionHistoryQuery{DateFrom=new DateOnly(2026,2,1),DateTo=new DateOnly(2026,1,1)},default)));}
 [Fact(DisplayName="FUNC063-USR-UTCID05")] public void Page_size_boundaries_are_normalized(){Assert.Equal(1,SessionHistoryQueryNormalizer.Normalize(new SessionHistoryQuery{Page=1,PageSize=1}).PageSize);Assert.Equal(50,SessionHistoryQueryNormalizer.Normalize(new SessionHistoryQuery{Page=1,PageSize=50}).PageSize);Assert.Throws<ArgumentException>(()=>SessionHistoryQueryNormalizer.Normalize(new SessionHistoryQuery{PageSize=51}));}
 [Fact(DisplayName="FUNC063-USR-UTCID06")] public async Task Cancellation_token_is_forwarded(){var(c,s,_,_,_)=MediaTest.Create();using var source=new CancellationTokenSource();s.Setup(x=>x.GetSessionHistoryAsync(It.IsAny<Guid>(),It.IsAny<SessionHistoryQuery>(),source.Token)).ReturnsAsync(new SessionHistoryResult());Assert.Equal(200,MediaTest.Status(await c.GetSessionHistory(new SessionHistoryQuery(),source.Token)));}
}

public class SessionDetailsUtcidTests
{
 [Fact(DisplayName="FUNC064-USR-UTCID01")] public async Task Returns_session_chat_details(){var(c,s,_,_,u)=MediaTest.Create();var id=Guid.NewGuid();s.Setup(x=>x.GetSessionChatLogAsync(u,id,It.IsAny<CancellationToken>())).ReturnsAsync(new SessionChatLogResponse{SessionId=id,Questions=[new SessionChatLogQuestionDto{QuestionText="Q"}]});Assert.Equal(200,MediaTest.Status(await c.GetChatLog(id,default)));}
 [Fact(DisplayName="FUNC064-USR-UTCID02")] public async Task Empty_chat_log_is_valid(){var(c,s,_,_,_)=MediaTest.Create();s.Setup(x=>x.GetSessionChatLogAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync(new SessionChatLogResponse());Assert.Equal(200,MediaTest.Status(await c.GetChatLog(Guid.NewGuid(),default)));}
 [Fact(DisplayName="FUNC064-USR-UTCID03")] public async Task Missing_identity_is_unauthorized(){var(c,s,_,_,_)=MediaTest.Create(false);Assert.Equal(401,MediaTest.Status(await c.GetChatLog(Guid.NewGuid(),default)));s.Verify(x=>x.GetSessionChatLogAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<CancellationToken>()),Times.Never);}
 [Fact(DisplayName="FUNC064-USR-UTCID04")] public async Task Missing_session_is_not_found(){var(c,s,_,_,_)=MediaTest.Create();s.Setup(x=>x.GetSessionChatLogAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException("missing"));Assert.Equal(404,MediaTest.Status(await c.GetChatLog(Guid.NewGuid(),default)));}
 [Fact(DisplayName="FUNC064-USR-UTCID05")] public async Task Guid_boundaries_are_forwarded(){var(c,s,_,_,_)=MediaTest.Create();s.Setup(x=>x.GetSessionChatLogAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync(new SessionChatLogResponse());Assert.Equal(200,MediaTest.Status(await c.GetChatLog(Guid.Empty,default)));Assert.Equal(200,MediaTest.Status(await c.GetChatLog(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),default)));}
 [Fact(DisplayName="FUNC064-USR-UTCID06")] public async Task Invalid_session_state_returns_conflict(){var(c,s,_,_,_)=MediaTest.Create();s.Setup(x=>x.GetSessionChatLogAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("state"));Assert.Equal(409,MediaTest.Status(await c.GetChatLog(Guid.NewGuid(),default)));}
}

public class SearchSessionsUtcidTests
{
 [Fact(DisplayName="FUNC065-USR-UTCID01")] public async Task Keyword_is_forwarded_to_history_search(){var(c,s,_,_,_)=MediaTest.Create();var q=new SessionHistoryQuery{Keyword="devprep"};s.Setup(x=>x.GetSessionHistoryAsync(It.IsAny<Guid>(),q,It.IsAny<CancellationToken>())).ReturnsAsync(new SessionHistoryResult{TotalItems=1});Assert.Equal(200,MediaTest.Status(await c.GetSessionHistory(q,default)));}
 [Fact(DisplayName="FUNC065-USR-UTCID02")] public async Task Whitespace_keyword_normalizes_to_null(){Assert.Null(SessionHistoryQueryNormalizer.Normalize(new SessionHistoryQuery{Keyword="   "}).Keyword);await Task.CompletedTask;}
 [Fact(DisplayName="FUNC065-USR-UTCID03")] public async Task Null_keyword_is_valid(){var(c,s,_,_,_)=MediaTest.Create();s.Setup(x=>x.GetSessionHistoryAsync(It.IsAny<Guid>(),It.IsAny<SessionHistoryQuery>(),It.IsAny<CancellationToken>())).ReturnsAsync(new SessionHistoryResult());Assert.Equal(200,MediaTest.Status(await c.GetSessionHistory(new SessionHistoryQuery{Keyword=null},default)));}
 [Fact(DisplayName="FUNC065-USR-UTCID04")] public void Invalid_status_filter_is_rejected(){Assert.Throws<ArgumentException>(()=>SessionHistoryQueryNormalizer.Normalize(new SessionHistoryQuery{Status="invalid-format"}));}
 [Fact(DisplayName="FUNC065-USR-UTCID05")] public void Text_and_status_boundaries_are_normalized(){var q=SessionHistoryQueryNormalizer.Normalize(new SessionHistoryQuery{Keyword=" x ",Status="completed",Page=1,PageSize=50});Assert.Equal("x",q.Keyword);Assert.Equal("Completed",q.Status);Assert.Equal(50,q.PageSize);}
 [Fact(DisplayName="FUNC065-USR-UTCID06")] public async Task Search_validation_error_returns_bad_request(){var(c,s,_,_,_)=MediaTest.Create();s.Setup(x=>x.GetSessionHistoryAsync(It.IsAny<Guid>(),It.IsAny<SessionHistoryQuery>(),It.IsAny<CancellationToken>())).ThrowsAsync(new ArgumentException("search"));Assert.Equal(400,MediaTest.Status(await c.GetSessionHistory(new SessionHistoryQuery{Keyword="bad"},default)));}
}
