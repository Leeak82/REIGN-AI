using REIGN.Web;
using Xunit;

namespace REIGN.Tests;

public class AdminLoginTests
{
    private const string CorrectHash = "pbkdf2-sha256$100000$cmVpZ24tbG9jYWwtZGV2IQ==$c9NNr27cHDefR0jf7cZOPqVKdTQwIUw3cfGccbF7SiI=";

    [Fact]
    public void Valid_credentials_are_accepted()
    {
        Assert.True(AdminLogin.IsValid("missreign", "correct-password", "missreign", CorrectHash));
        Assert.True(AdminLogin.IsValid(" MISSREIGN ", "correct-password", "missreign", CorrectHash));
    }

    [Theory]
    [InlineData("wrong", "correct-password")]
    [InlineData("missreign", "wrong-password")]
    [InlineData("missreign", "")]
    [InlineData("", "correct-password")]
    public void Invalid_credentials_are_rejected(string username, string password)
    {
        Assert.False(AdminLogin.IsValid(username, password, "missreign", CorrectHash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("pbkdf2-sha256$99999$cmVpZ24tbG9jYWwtZGV2IQ==$c9NNr27cHDefR0jf7cZOPqVKdTQwIUw3cfGccbF7SiI=")]
    public void Invalid_hashes_are_rejected(string hash)
    {
        Assert.False(AdminLogin.VerifyPassword("correct-password", hash));
    }
}
