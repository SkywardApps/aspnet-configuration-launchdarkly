using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;

namespace SkywardApps.AspnetConfiguration.AwsSecrets
{
    public class AwsSecretsConfigurationProviderOptions
    {
        public string SecretArn { get; set; }
        public AwsSecretsConfigurationProviderOptions(string secretArn)
        {
            SecretArn = secretArn ?? throw new ArgumentNullException(nameof(secretArn));
        }
    }

    public class AwsSecretsConfigurationProvider : ConfigurationProvider
    {
        private readonly AwsSecretsConfigurationProviderOptions _options;
        private readonly Timer _timer;
        private readonly object _lockObject = new();
        private DateTime? _lastUpdate;

        private readonly int _maxRefreshSeconds = 60;

        public AwsSecretsConfigurationProvider(AwsSecretsConfigurationProviderOptions options)
        {
            _options = options;
            // Set up a timer to fetch secrets periodically (every minute in this case)
            _timer = new Timer
            (
                callback: _ =>
                {
                    Load();
                    OnReload();
                },
                dueTime: TimeSpan.FromSeconds(_maxRefreshSeconds * 2),
                period: TimeSpan.FromSeconds(_maxRefreshSeconds),
                state: null
            );
        }

        public override void Load()
        {
            // Skip trying to load if the object is locked.
            if (!Monitor.TryEnter(_lockObject))
            {
                return;
            }

            try
            {
                // Only refresh at specified intervals.
                if (_lastUpdate != null && (DateTime.Now - _lastUpdate.Value).TotalSeconds < _maxRefreshSeconds)
                {
                    return;
                }

                Data = FetchSecretAsync().GetAwaiter().GetResult();
                _lastUpdate = DateTime.Now;
            }
            catch (Exception e)
            {
                // Ignore all exceptions.
                // Cannot use logger because the logger might not be initialized yet.
                Console.WriteLine("An exception occured while loading secrets:");
                Console.WriteLine(e.Message);
            }
            finally
            {
                Monitor.Exit(_lockObject);
            }
        }

        /// <summary>
        /// Make key retrieval case insensitive.
        /// </summary>
        public override bool TryGet(string key, out string? value)
        {
            if (Data.ContainsKey(key.ToLower()))
            {
                value = Data[key.ToLower()];
                return true;
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Fetches AWS secret and mangles the value into a format ASPnet configuration can figure out.
        /// </summary>
        private async Task<Dictionary<string, string?>> FetchSecretAsync()
        {
            // https://github.com/awsdocs/aws-doc-sdk-examples/blob/main/dotnetv3/SecretsManager/GetSecretValue/GetSecretValueExample/GetSecretValue.cs
            var client = new AmazonSecretsManagerClient();

            var secret = await client.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = _options.SecretArn
            });

            var overrides = JsonSerializer.Deserialize<Dictionary<string, string?>>(secret.SecretString);
            if (overrides == null)
            {
                return new Dictionary<string, string?>();
            }
            var corrected = overrides.ToDictionary(kv => kv.Key.Replace("__", ":").ToLower(), kv => kv.Value);

            return corrected;
        }
    }

    public class AwsSecretsConfigurationProviderSource : IConfigurationSource
    {
        private readonly AwsSecretsConfigurationProviderOptions _options;

        public AwsSecretsConfigurationProviderSource(AwsSecretsConfigurationProviderOptions options)
        {
            _options = options;
        }

        private static AwsSecretsConfigurationProvider? provider;
        private static AwsSecretsConfigurationProvider GetProvider(AwsSecretsConfigurationProviderOptions options)
        {
            provider ??= new AwsSecretsConfigurationProvider(options);
            return provider;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return GetProvider(_options);
        }
    }

    public static class AwsSecretsConfigurationExtensions
    {
        public static IConfigurationBuilder AddAwsSecrets(this IConfigurationBuilder builder, AwsSecretsConfigurationProviderOptions? options)
        {
            if (options != null)
            {
                return builder.Add(new AwsSecretsConfigurationProviderSource(options));
            }

            var secretArn = Environment.GetEnvironmentVariable("APPSETTINGS_OVERRIDE_SECRET_ARN");
            if (secretArn == null)
            {
                Console.WriteLine("No AWS secret provided. AWS configuration overrides disabled.");
                return builder;
            }

            return builder.Add(new AwsSecretsConfigurationProviderSource(new AwsSecretsConfigurationProviderOptions(secretArn)));
        }
    }
}
