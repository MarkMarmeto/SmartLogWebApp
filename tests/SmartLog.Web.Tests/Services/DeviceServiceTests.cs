using SmartLog.Web.Services;

namespace SmartLog.Web.Tests.Services;

public class DeviceServiceTests
{
    private readonly DeviceService _service = new();

    [Fact]
    public void GenerateApiKey_StartsWithPrefix()
    {
        var key = _service.GenerateApiKey();
        Assert.StartsWith("sk_live_", key);
    }

    [Fact]
    public void GenerateApiKey_IsUnique()
    {
        var key1 = _service.GenerateApiKey();
        var key2 = _service.GenerateApiKey();
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void GenerateApiKey_IsUrlSafe()
    {
        var key = _service.GenerateApiKey();
        Assert.DoesNotContain("+", key);
        Assert.DoesNotContain("/", key);
        Assert.DoesNotContain("=", key);
    }

    [Fact]
    public void HashApiKey_ReturnsDeterministicHash()
    {
        var key = "sk_live_testkey123";
        var hash1 = _service.HashApiKey(key);
        var hash2 = _service.HashApiKey(key);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void HashApiKey_DifferentKeysProduceDifferentHashes()
    {
        var hash1 = _service.HashApiKey("sk_live_key1");
        var hash2 = _service.HashApiKey("sk_live_key2");
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void VerifyApiKey_MatchingKey_ReturnsTrue()
    {
        var key = _service.GenerateApiKey();
        var hash = _service.HashApiKey(key);
        Assert.True(_service.VerifyApiKey(key, hash));
    }

    [Fact]
    public void VerifyApiKey_WrongKey_ReturnsFalse()
    {
        var key = _service.GenerateApiKey();
        var hash = _service.HashApiKey(key);
        Assert.False(_service.VerifyApiKey("sk_live_wrong", hash));
    }
}
