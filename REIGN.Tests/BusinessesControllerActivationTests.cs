using Microsoft.Extensions.Configuration;
using REIGN.API.Controllers;
using REIGN.API.Services;
using REIGN.Data;
using Xunit;

namespace REIGN.Tests;

public class BusinessesControllerActivationTests
{
    [Fact]
    public void Businesses_controller_has_one_unambiguous_public_constructor()
    {
        var constructors = typeof(BusinessesController).GetConstructors();
        var constructor = Assert.Single(constructors);
        var parameterTypes = constructor.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

        Assert.Equal(
            new[] { typeof(ReignDbContext), typeof(IBusinessProfileAccessor), typeof(IConfiguration) },
            parameterTypes);
    }
}
