using Microsoft.Extensions.Configuration;

#nullable enable
namespace Skyward.Aspnet.Configuration
{
    public static class LaunchDarklyConfigurationExtensions
    {
        public static IConfigurationBuilder AddLaunchDarklyConfiguration(
            this IConfigurationBuilder builder, string sdkKey, string prefix = "configure-backend-")
        {
            return builder.Add(new LaunchDarklyConfigurationSource(sdkKey, prefix));
        }
    }
}
