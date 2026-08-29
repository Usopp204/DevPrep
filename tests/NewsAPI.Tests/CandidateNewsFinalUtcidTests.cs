using NewsAPI.DTOs;
using NewsAPI.Tests.Controllers;
using Xunit;

namespace NewsAPI.Tests;

public class CandidateNewsFinalUtcidTests
{
 [Theory]
 [InlineData("FUNC193-USR-UTCID01",1)][InlineData("FUNC193-USR-UTCID02",2)][InlineData("FUNC193-USR-UTCID03",3)][InlineData("FUNC193-USR-UTCID04",4)][InlineData("FUNC193-USR-UTCID05",5)][InlineData("FUNC193-USR-UTCID06",6)]
 public async Task Filter_news(string caseId,int scenario)
 {
  var category=scenario is 1 or 3?Guid.NewGuid():(Guid?)null;
  var query=new NewsQueryDto{CategoryId=category,Page=scenario==5?int.MaxValue:1,PageSize=scenario==4?1:10};
  await PublicNewsTest.AssertList(query,scenario==2?0:1,q=>Assert.Equal(category,q.CategoryId));
  Assert.StartsWith("FUNC193-USR-UTCID",caseId);
 }

 [Theory]
 [InlineData("FUNC194-USR-UTCID01","dotnet")][InlineData("FUNC194-USR-UTCID02","  AI  ")][InlineData("FUNC194-USR-UTCID03","")][InlineData("FUNC194-USR-UTCID04","missing")][InlineData("FUNC194-USR-UTCID05","x")][InlineData("FUNC194-USR-UTCID06","xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
 public async Task Search_news(string caseId,string keyword)
 {
  await PublicNewsTest.AssertList(new NewsQueryDto{Keyword=keyword},keyword=="missing"?0:1,q=>Assert.Equal(keyword,q.Keyword));
  Assert.StartsWith("FUNC194-USR-UTCID",caseId);
 }
}
