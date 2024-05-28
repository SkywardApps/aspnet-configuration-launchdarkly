using System;
using System.Runtime.Serialization;

namespace Skyward.Aspnet.Configuration
{
    [Serializable]
    internal class LaunchDarklyConfigurationException : Exception
    {
        public LaunchDarklyConfigurationException()
        {
        }

        public LaunchDarklyConfigurationException(string message) : base(message)
        {
        }

        public LaunchDarklyConfigurationException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected LaunchDarklyConfigurationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}