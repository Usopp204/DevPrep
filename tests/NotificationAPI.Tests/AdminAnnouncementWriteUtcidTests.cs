using Microsoft.AspNetCore.Http;
using Moq;
using NotificationAPI.Endpoints;
using NotificationAPI.Models;
using NotificationAPI.Repositories.Interfaces;
using Xunit;

namespace NotificationAPI.Tests;

public class AdminAnnouncementWriteUtcidTests
{
    [Fact(DisplayName="FUNC152-ADM-UTCID01")]
    public async Task Create_adds_active_announcement_with_timestamps()
    {
        var repo=Repo(); Announcement? saved=null; repo.Setup(x=>x.AddAsync(It.IsAny<Announcement>())).Callback<Announcement>(x=>saved=x).Returns(Task.CompletedTask);
        Assert.Equal(200,Status(await AnnouncementWriteEndpoints.CreateAsync(new("Title","Content"),repo.Object)));
        Assert.NotNull(saved); Assert.True(saved.IsActive); Assert.False(saved.IsDeleted); Assert.NotEqual(Guid.Empty,saved.AnnouncementId); Assert.Equal(saved.CreatedAt,saved.UpdatedAt); repo.Verify(x=>x.SaveChangesAsync(),Times.Once);
    }
    [Fact(DisplayName="FUNC152-ADM-UTCID02")]
    public async Task Create_trims_text_and_preserves_inactive_flag()
    {
        var repo=Repo(); Announcement? saved=null; repo.Setup(x=>x.AddAsync(It.IsAny<Announcement>())).Callback<Announcement>(x=>saved=x).Returns(Task.CompletedTask);
        await AnnouncementWriteEndpoints.CreateAsync(new("  Title  ","  Body  ",false),repo.Object); Assert.Equal("Title",saved!.Title); Assert.Equal("Body",saved.Content); Assert.False(saved.IsActive);
    }
    [Fact(DisplayName="FUNC152-ADM-UTCID03")]
    public async Task Create_rejects_blank_title_without_writing()
    {
        var repo=Repo(); Assert.Equal(400,Status(await AnnouncementWriteEndpoints.CreateAsync(new(" ","Body"),repo.Object))); repo.Verify(x=>x.AddAsync(It.IsAny<Announcement>()),Times.Never);
    }
    [Fact(DisplayName="FUNC152-ADM-UTCID04")]
    public async Task Create_rejects_blank_content()
    {
        var repo=Repo(); Assert.Equal(400,Status(await AnnouncementWriteEndpoints.CreateAsync(new("Title"," "),repo.Object))); repo.Verify(x=>x.SaveChangesAsync(),Times.Never);
    }
    [Fact(DisplayName="FUNC152-ADM-UTCID05")]
    public async Task Create_accepts_maximum_title_and_content_boundaries()
    {
        var repo=Repo(); repo.Setup(x=>x.AddAsync(It.IsAny<Announcement>())).Returns(Task.CompletedTask);
        Assert.Equal(200,Status(await AnnouncementWriteEndpoints.CreateAsync(new(new string('t',200),new string('c',2000)),repo.Object)));
    }
    [Fact(DisplayName="FUNC152-ADM-UTCID06")]
    public async Task Create_propagates_repository_failure()
    {
        var repo=Repo(); repo.Setup(x=>x.AddAsync(It.IsAny<Announcement>())).ThrowsAsync(new InvalidOperationException("insert failed"));
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>AnnouncementWriteEndpoints.CreateAsync(new("Title","Body"),repo.Object)); Assert.Equal("insert failed",error.Message);
    }
    [Fact(DisplayName="FUNC153-ADM-UTCID01")]
    public async Task Update_changes_fields_and_timestamp()
    {
        var item=Item(); var old=item.UpdatedAt; var repo=Repo(); repo.Setup(x=>x.GetTrackedByIdAsync(item.AnnouncementId)).ReturnsAsync(item);
        Assert.Equal(200,Status(await AnnouncementWriteEndpoints.UpdateAsync(item.AnnouncementId,new(" New "," Body ",false),repo.Object)));
        Assert.Equal("New",item.Title); Assert.Equal("Body",item.Content); Assert.False(item.IsActive); Assert.True(item.UpdatedAt>old); repo.Verify(x=>x.SaveChangesAsync(),Times.Once);
    }
    [Fact(DisplayName="FUNC153-ADM-UTCID02")]
    public async Task Update_returns_not_found_for_unknown_id()
    {
        var repo=Repo(); Assert.Equal(404,Status(await AnnouncementWriteEndpoints.UpdateAsync(Guid.NewGuid(),new("Title","Body"),repo.Object))); repo.Verify(x=>x.SaveChangesAsync(),Times.Never);
    }
    [Fact(DisplayName="FUNC153-ADM-UTCID03")]
    public async Task Update_returns_not_found_for_deleted_item()
    {
        var item=Item(); item.IsDeleted=true; var repo=Repo(); repo.Setup(x=>x.GetTrackedByIdAsync(item.AnnouncementId)).ReturnsAsync(item);
        Assert.Equal(404,Status(await AnnouncementWriteEndpoints.UpdateAsync(item.AnnouncementId,new("Title","Body"),repo.Object)));
    }
    [Fact(DisplayName="FUNC153-ADM-UTCID04")]
    public async Task Update_validates_before_repository_lookup()
    {
        var repo=Repo(); Assert.Equal(400,Status(await AnnouncementWriteEndpoints.UpdateAsync(Guid.NewGuid(),new("","Body"),repo.Object))); repo.Verify(x=>x.GetTrackedByIdAsync(It.IsAny<Guid>()),Times.Never);
    }
    [Fact(DisplayName="FUNC153-ADM-UTCID05")]
    public async Task Update_rejects_values_over_boundaries()
    {
        var repo=Repo(); Assert.Equal(400,Status(await AnnouncementWriteEndpoints.UpdateAsync(Guid.Empty,new(new string('x',201),"Body"),repo.Object))); Assert.Equal(400,Status(await AnnouncementWriteEndpoints.UpdateAsync(Guid.Empty,new("Title",new string('x',2001)),repo.Object)));
    }
    [Fact(DisplayName="FUNC153-ADM-UTCID06")]
    public async Task Update_propagates_save_failure_after_mutation()
    {
        var item=Item(); var repo=Repo(); repo.Setup(x=>x.GetTrackedByIdAsync(item.AnnouncementId)).ReturnsAsync(item); repo.Setup(x=>x.SaveChangesAsync()).ThrowsAsync(new TimeoutException());
        await Assert.ThrowsAsync<TimeoutException>(()=>AnnouncementWriteEndpoints.UpdateAsync(item.AnnouncementId,new("New","Body"),repo.Object)); Assert.Equal("New",item.Title);
    }
    [Fact(DisplayName="FUNC154-ADM-UTCID01")]
    public async Task Delete_soft_deletes_and_saves()
    {
        var item=Item(); var repo=Repo(); repo.Setup(x=>x.GetTrackedByIdAsync(item.AnnouncementId)).ReturnsAsync(item);
        Assert.Equal(204,Status(await AnnouncementWriteEndpoints.DeleteAsync(item.AnnouncementId,repo.Object))); Assert.True(item.IsDeleted); Assert.NotNull(item.DeletedAt); Assert.Equal(item.DeletedAt,item.UpdatedAt); repo.Verify(x=>x.SaveChangesAsync(),Times.Once);
    }
    [Fact(DisplayName="FUNC154-ADM-UTCID02")]
    public async Task Delete_returns_not_found_for_unknown_id()
    {
        var repo=Repo(); Assert.Equal(404,Status(await AnnouncementWriteEndpoints.DeleteAsync(Guid.NewGuid(),repo.Object))); repo.Verify(x=>x.SaveChangesAsync(),Times.Never);
    }
    [Fact(DisplayName="FUNC154-ADM-UTCID03")]
    public async Task Delete_is_idempotent_for_already_deleted_item()
    {
        var item=Item(); item.IsDeleted=true; item.DeletedAt=DateTime.UtcNow.AddDays(-1); var original=item.DeletedAt; var repo=Repo(); repo.Setup(x=>x.GetTrackedByIdAsync(item.AnnouncementId)).ReturnsAsync(item);
        Assert.Equal(204,Status(await AnnouncementWriteEndpoints.DeleteAsync(item.AnnouncementId,repo.Object))); Assert.Equal(original,item.DeletedAt); repo.Verify(x=>x.SaveChangesAsync(),Times.Never);
    }
    [Fact(DisplayName="FUNC154-ADM-UTCID04")]
    public async Task Delete_empty_guid_boundary_is_forwarded()
    {
        var repo=Repo(); Assert.Equal(404,Status(await AnnouncementWriteEndpoints.DeleteAsync(Guid.Empty,repo.Object))); repo.Verify(x=>x.GetTrackedByIdAsync(Guid.Empty),Times.Once);
    }
    [Fact(DisplayName="FUNC154-ADM-UTCID05")]
    public async Task Delete_preserves_content_and_activation_fields()
    {
        var item=Item(); var repo=Repo(); repo.Setup(x=>x.GetTrackedByIdAsync(item.AnnouncementId)).ReturnsAsync(item); await AnnouncementWriteEndpoints.DeleteAsync(item.AnnouncementId,repo.Object);
        Assert.Equal("Old",item.Title); Assert.Equal("Old body",item.Content); Assert.True(item.IsActive);
    }
    [Fact(DisplayName="FUNC154-ADM-UTCID06")]
    public async Task Delete_propagates_save_failure()
    {
        var item=Item(); var repo=Repo(); repo.Setup(x=>x.GetTrackedByIdAsync(item.AnnouncementId)).ReturnsAsync(item); repo.Setup(x=>x.SaveChangesAsync()).ThrowsAsync(new InvalidOperationException("delete failed"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>AnnouncementWriteEndpoints.DeleteAsync(item.AnnouncementId,repo.Object));
    }
    private static Mock<IAnnouncementRepository> Repo(){var repo=new Mock<IAnnouncementRepository>();repo.Setup(x=>x.SaveChangesAsync()).ReturnsAsync(1);return repo;}
    private static Announcement Item()=>new(){AnnouncementId=Guid.NewGuid(),Title="Old",Content="Old body",IsActive=true,CreatedAt=DateTime.UtcNow.AddDays(-2),UpdatedAt=DateTime.UtcNow.AddDays(-1)};
    private static int Status(IResult result)=>Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode!.Value;
}
