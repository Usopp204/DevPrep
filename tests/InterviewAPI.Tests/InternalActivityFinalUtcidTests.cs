using System.Text.Json;
using InterviewAPI.Controllers;
using InterviewAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InterviewAPI.Tests;

public class InternalActivityFinalUtcidTests
{
 [Theory]
 [InlineData("FUNC204-USR-UTCID01",1)][InlineData("FUNC204-USR-UTCID02",2)][InlineData("FUNC204-USR-UTCID03",3)][InlineData("FUNC204-USR-UTCID04",4)][InlineData("FUNC204-USR-UTCID05",5)][InlineData("FUNC204-USR-UTCID06",6)]
 public async Task Last_interview_activity(string id,int n)
 {
  await using var db=new InterviewDbContext(new DbContextOptionsBuilder<InterviewDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
  var user=Guid.NewGuid();var other=Guid.NewGuid();var old=Session(user,DateTime.UtcNow.AddDays(-2));var latest=Session(user,DateTime.UtcNow.AddDays(-1));
  if(n>=3)db.InterviewSessions.AddRange(old,latest);
  if(n==4){latest.IsDeleted=true;}
  if(n==5){old.StartedAt=null;old.CreatedAt=DateTime.UtcNow.AddYears(-50);}
  if(n==6)db.InterviewSessions.Add(Session(other,DateTime.UtcNow));
  await db.SaveChangesAsync();
  List<Guid>? ids=n switch{1=>null,2=>[],6=>[user,other],_=>[user]};
  var ok=Assert.IsType<OkObjectResult>(await new InternalInterviewActivityController(db).GetLastActivity(ids!));
  var json=JsonSerializer.Serialize(ok.Value);
  Assert.Equal(n<=2?0:n==4?1:n==6?2:1,JsonDocument.Parse(json).RootElement.GetArrayLength());
  Assert.StartsWith("FUNC204-USR-UTCID",id);
 }
 static InterviewSession Session(Guid user,DateTime at)=>new(){SessionId=Guid.NewGuid(),UserId=user,Level="Junior",InteractionMode="Text",QuestionType="Mixed",Status="Completed",StartedAt=at,CreatedAt=at,UpdatedAt=at};
}
