using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ThreadListParamsSerializationTests
{
    [Fact]
    public void Serialize_UsesLimitFieldName_NotPageSize()
    {
        var json = JsonSerializer.Serialize(
            new ThreadListParams { Limit = 10 },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"limit\":10");
        json.Should().NotContain("isPinned");
        json.Should().NotContain("pageSize");
    }

    [Fact]
    public void Serialize_WritesExplicitNullSectionFilter()
    {
        var json = JsonSerializer.Serialize(
            new ThreadListParams { SectionId = JsonSerializer.SerializeToElement((string?)null) },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"sectionId\":null");
    }

    [Fact]
    public void Serialize_WritesSectionIdFilter()
    {
        var json = JsonSerializer.Serialize(
            new ThreadListParams { SectionId = JsonSerializer.SerializeToElement("sec_1") },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"sectionId\":\"sec_1\"");
    }

    [Fact]
    public void Serialize_WritesProjectIdFilters()
    {
        var projectJson = JsonSerializer.Serialize(
            new ThreadListParams { ProjectId = JsonSerializer.SerializeToElement("proj_1") },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        projectJson.Should().Contain("\"projectId\":\"proj_1\"");

        var unassignedJson = JsonSerializer.Serialize(
            new ThreadListParams { ProjectId = JsonSerializer.SerializeToElement((string?)null) },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        unassignedJson.Should().Contain("\"projectId\":null");
    }
}
