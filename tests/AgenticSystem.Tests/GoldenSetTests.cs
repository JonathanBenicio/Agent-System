using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using Xunit;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Persistence.Entities;
using AgenticSystem.Infrastructure.Persistence;

namespace AgenticSystem.Tests;

public class GoldenSetTests
{
    [Fact]
    public void EntityMapping_ShouldPreserveAllProperties()
    {
        // Arrange
        var domain = new GoldenSet
        {
            Id = "gs_1",
            TenantId = "tenant_1",
            Name = "Test Golden Set",
            Description = "Test Description",
            AgentName = "agent_1",
            Cases = new List<GoldenSetCase>
            {
                new GoldenSetCase
                {
                    Id = "case_1",
                    Input = "Test Input",
                    ExpectedOutput = "Expected output text",
                    Tags = new List<string> { "tag1", "tag2" }
                }
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var entity = GoldenSetEntity.FromModel(domain);
        var mappedBack = entity.ToModel();

        // Assert
        mappedBack.Id.Should().Be(domain.Id);
        mappedBack.TenantId.Should().Be(domain.TenantId);
        mappedBack.Name.Should().Be(domain.Name);
        mappedBack.Description.Should().Be(domain.Description);
        mappedBack.AgentName.Should().Be(domain.AgentName);
        mappedBack.Cases.Should().HaveCount(1);
        mappedBack.Cases[0].Id.Should().Be(domain.Cases[0].Id);
        mappedBack.Cases[0].Input.Should().Be(domain.Cases[0].Input);
        mappedBack.Cases[0].ExpectedOutput.Should().Be(domain.Cases[0].ExpectedOutput);
        mappedBack.Cases[0].Tags.Should().ContainInOrder(domain.Cases[0].Tags);
    }

    [Fact]
    public async Task InMemoryRepository_ShouldStoreAndRetrieveSuccessfully()
    {
        // Arrange
        var repo = new InMemoryGoldenSetRepository();
        var goldenSet = new GoldenSet
        {
            Id = "gs_inmemory_1",
            TenantId = "tenant_1",
            Name = "In Memory Test Set",
            AgentName = "test_agent",
            Cases = new List<GoldenSetCase>
            {
                new GoldenSetCase { Id = "c1", Input = "Hello", ExpectedOutput = "Hi" }
            }
        };

        // Act
        await repo.AddAsync(goldenSet);
        var retrieved = await repo.GetByIdAsync("gs_inmemory_1", "tenant_1");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("In Memory Test Set");
        retrieved.AgentName.Should().Be("test_agent");
    }

    [Fact]
    public async Task InMemoryRepository_List_ShouldFilterAndPageCorrectly()
    {
        // Arrange
        var repo = new InMemoryGoldenSetRepository();
        var tenant = "tenant_page_test";

        for (int i = 1; i <= 5; i++)
        {
            await repo.AddAsync(new GoldenSet
            {
                Id = $"gs_{i}",
                TenantId = tenant,
                Name = $"Set {i}",
                AgentName = i % 2 == 0 ? "agent_even" : "agent_odd",
                Cases = new List<GoldenSetCase>
                {
                    new GoldenSetCase { Id = $"c{i}", Input = $"Input {i}" }
                }
            });
        }

        // Act & Assert (Filter by Agent)
        var evenList = await repo.ListAsync(tenant, "agent_even", 1, 10);
        evenList.Should().HaveCount(2);

        // Act & Assert (Paging)
        var page1 = await repo.ListAsync(tenant, null, 1, 3);
        page1.Should().HaveCount(3);

        var page2 = await repo.ListAsync(tenant, null, 2, 3);
        page2.Should().HaveCount(2);
    }
}
