using System;
using Microsoft.Extensions.Configuration;

#nullable enable
namespace Skyward.Aspnet.Configuration
{
    class LaunchDarklyConfigurationSource : IConfigurationSource
    {
        private readonly string _sdkKey;
        private readonly string _prefix;

        public LaunchDarklyConfigurationSource(string sdkKey, string prefix)
        {
            _sdkKey = sdkKey ?? throw new ArgumentNullException(nameof(sdkKey));
            _prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new LaunchDarklyConfigurationProvider(_sdkKey, _prefix);
        }
    }
}
