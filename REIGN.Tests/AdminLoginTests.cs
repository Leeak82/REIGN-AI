using REIGN.Web;
using Xunit;

namespace REIGN.Tests;

public class AdminLoginTests
{
    [Fact]
    public void Valid_credentials_are_accepted()
    {
        Assert.True(AdminLogin.IsValid("missreign", "correct-password", "missreign", "correct-password"));
        Assert.True(AdminLogin.IsValid(" MISSREIGN ", "correct-password", "missreign", "correct-password"));
    }

    [Theory]
    [InlineData("wrong", "correct-password")]
    [InlineData("missreign", "wrong-password")]
    [InlineData("missreign", "")]
    [InlineData("", "correct-password")]
    public void Invalid_credentials_are_rejected(string username, string password)
    {
        Assert.False(AdminLogin.IsValid(username, password, "missreign", "correct-password"));
    }
}
