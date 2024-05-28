using System;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Configuration;
using LaunchDarkly.Sdk.Server;
using LaunchDarkly.Sdk;

#nullable enable
namespace Skyward.Aspnet.Configuration
{
    public class LaunchDarklyConfigurationProvider : ConfigurationProvider
    {
        private readonly string _sdkKey;
        private readonly string _prefix;
        LdClient? _ldClient = null;
        SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public LaunchDarklyConfigurationProvider(string sdkKey, string prefix)
        {
            _sdkKey = sdkKey ?? throw new ArgumentNullException(nameof(sdkKey));
            _prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));
        }

        public override void Load()
        {
            if (_sdkKey == null)
            {
                return;
            }

            _lock.Wait();
            try
            {
                if (_ldClient == null)
                {
                    var ldConfig = LaunchDarkly.Sdk.Server.Configuration.Builder(_sdkKey)
                        .DiagnosticOptOut(true)
                        .Build();
                    _ldClient = new LdClient(ldConfig);
                    _ldClient.FlagTracker.FlagChanged += (sender, args) =>
                    {
                        this.Load();
                    };
                }

                var state = _ldClient.AllFlagsState(Context.Builder("anon").Anonymous(true).Kind(ContextKind.Default).Build());
                if (!state.Valid)
                {
                    if (this.Data == null || !this.Data.Any())
                    {
                        // What do we do in this case? First load => fail, later loads => reuse?
                        throw new LaunchDarklyConfigurationException("Could not load our initial flags");
                    }
                }
                else
                {
                    var data = state.ToValuesJsonMap();
                    if (data != null)
                    {
                        this.Data = data
                            .Where(kv => kv.Key.StartsWith(_prefix) && kv.Value != null)
                            .ToDictionary(kv => kv.Key.Substring(_prefix.Length).Replace("-", ":"), kv => (string?)(kv.Value.IsString
                                ? kv.Value.AsString
                                : kv.Value.ToJsonString()));
                    }
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public override bool TryGet(string key, out string? value)
        {
            if (this.Data.ContainsKey(key.ToLower()))
            {
                value = this.Data[key.ToLower()]!;
                return true;
            }

            value = null;
            return false;
        }

    }
}
