using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class ConfigManagerTests
{
    private readonly InMemoryConfigStore _store;
    private readonly AesConfigEncryptionService _encryption;
    private readonly ConfigReloadNotifier _notifier;
    private readonly ConfigManager _sut;

    public ConfigManagerTests()
    {
        _store = new InMemoryConfigStore();
        _encryption = new AesConfigEncryptionService("test-key-for-unit-tests-32ch!");
        _notifier = new ConfigReloadNotifier();
        var logger = Substitute.For<ILogger<ConfigManager>>();
        _sut = new ConfigManager(_store, _encryption, _notifier, logger);
    }

    [Fact]
    public async Task SetAsync_NonSecret_StoresPlainValue()
    {
        var request = new ConfigEntryRequest
        {
            Key = "app:url",
            Value = "https://localhost:5000",
            IsSecret = false,
            Category = ConfigCategory.General,
            Description = "App base URL"
        };

        var result = await _sut.SetAsync(request);

        result.Should().NotBeNull();
        result.Key.Should().Be("app:url");
        result.Value.Should().Be("https://localhost:5000");
        result.IsSecret.Should().BeFalse();
    }

    [Fact]
    public async Task SetAsync_Secret_MasksValue()
    {
        var request = new ConfigEntryRequest
        {
            Key = "openai:apikey",
            Value = "sk-test-12345",
            IsSecret = true,
            Category = ConfigCategory.Credentials,
            Description = "OpenAI API Key"
        };

        var result = await _sut.SetAsync(request);

        result.Should().NotBeNull();
        result.IsSecret.Should().BeTrue();
        result.Value.Should().Be("********");
        result.EncryptedValue.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAsync_Secret_ReturnsMasked()
    {
        await _sut.SetAsync(new ConfigEntryRequest
        {
            Key = "db:password",
            Value = "super-secret-pwd",
            IsSecret = true,
            Category = ConfigCategory.Connection
        });

        var entry = await _sut.GetAsync("db:password");

        entry.Should().NotBeNull();
        entry.Value.Should().Be("********");
        entry.IsSecret.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllAsync_FiltersByCategory()
    {
        await _sut.SetAsync(new ConfigEntryRequest { Key = "cred1", Value = "v1", Category = ConfigCategory.Credentials });
        await _sut.SetAsync(new ConfigEntryRequest { Key = "path1", Value = "/tmp", Category = ConfigCategory.Paths });
        await _sut.SetAsync(new ConfigEntryRequest { Key = "cred2", Value = "v2", Category = ConfigCategory.Credentials });

        var creds = await _sut.GetAllAsync(ConfigCategory.Credentials);

        creds.Should().HaveCount(2);
        creds.Should().AllSatisfy(e => e.Category.Should().Be(ConfigCategory.Credentials));
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntry()
    {
        await _sut.SetAsync(new ConfigEntryRequest { Key = "to-delete", Value = "v" });

        var act = () => _sut.DeleteAsync("to-delete");

        await act.Should().NotThrowAsync();
        var storeEntry = await _store.GetByKeyAsync("to-delete");
        storeEntry.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistent_ThrowsKeyNotFound()
    {
        var act = () => _sut.DeleteAsync("non-existent-key");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ValidateAsync_ValidEntry_ReturnsValid()
    {
        await _sut.SetAsync(new ConfigEntryRequest
        {
            Key = "valid:key",
            Value = "some-value",
            Category = ConfigCategory.General
        });

        var result = await _sut.ValidateAsync("valid:key");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_NonExistent_ReturnsFalse()
    {
        var result = await _sut.ValidateAsync("missing");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task GetAuditLogAsync_TracksChanges()
    {
        await _sut.SetAsync(new ConfigEntryRequest { Key = "audit-test", Value = "v1" });
        await _sut.UpdateAsync("audit-test", new ConfigEntryRequest { Key = "audit-test", Value = "v2" });

        var logs = await _sut.GetAuditLogAsync("audit-test");

        logs.Should().HaveCountGreaterThanOrEqualTo(2);
        logs.Should().AllSatisfy(l => l.ConfigKey.Should().Be("audit-test"));
    }

    [Fact]
    public async Task SetAsync_NotifiesReload()
    {
        string? notifiedKey = null;
        _notifier.OnChange(key => notifiedKey = key);

        await _sut.SetAsync(new ConfigEntryRequest { Key = "notify-test", Value = "v" });

        notifiedKey.Should().Be("notify-test");
    }

    [Fact]
    public async Task UpdateAsync_ChangesValue()
    {
        await _sut.SetAsync(new ConfigEntryRequest { Key = "upd", Value = "original", Category = ConfigCategory.General });

        var updated = await _sut.UpdateAsync("upd", new ConfigEntryRequest { Key = "upd", Value = "changed" });

        updated.Value.Should().Be("changed");
    }
}

public class AesConfigEncryptionServiceTests
{
    private readonly AesConfigEncryptionService _sut = new("my-test-encryption-key-32chars!!");

    [Fact]
    public void Encrypt_Decrypt_RoundTrip()
    {
        var plaintext = "super-secret-value";

        var encrypted = _sut.Encrypt(plaintext);
        var decrypted = _sut.Decrypt(encrypted);

        decrypted.Should().Be(plaintext);
        encrypted.Should().NotBe(plaintext);
    }

    [Fact]
    public void Hash_SameInput_SameHash()
    {
        var hash1 = _sut.Hash("value");
        var hash2 = _sut.Hash("value");

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void Hash_DifferentInput_DifferentHash()
    {
        var hash1 = _sut.Hash("value1");
        var hash2 = _sut.Hash("value2");

        hash1.Should().NotBe(hash2);
    }
}
