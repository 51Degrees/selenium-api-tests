using System;
using System.IO;
using FiftyOne.Common.TestHelpers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace FiftyOne.Pipeline.Cloud.Tests.Common
{
    // This class represents the configuration of the test environment
    public class TestConfig
    {
        // Root URL used for testing
        public string RootUrl { get; }
        // Free resource key no properties
        public string FreeResourceKey { get; }
        // Free resource key with properties
        public string FreeResourceKeyProps { get; }
        // Paid resource key no properties
        public string PaidResourceKey { get; }
        // Paid resource key with properties
        public string PaidResourceKeyProps { get; }
        // Resource key whose subscription has expired in Chargify
        public string ExpiredResourceKey { get; }
        // Resource key on a plan with zero remaining quota
        public string ZeroQuotaResourceKey { get; }
        // Resource key manually restricted (e.g. ToS breach)
        public string RestrictedResourceKey { get; }
        // Resource key restricted to a specific referer domain (*.test.com)
        public string DomainRestrictedResourceKey { get; }
        // FODiD-enabled resource key
        public string FodidResourceKey { get; }
        // Free V1 license
        public string FreeLicense { get; }
        // Premium V1 license
        public string PremiumLicense { get; }
        // Free V4 license
        public string FreeV4License { get; }
        // Enterprise V4 license
        public string EnterpriseV4License { get; }
        // Cloud V5 Bespoke
        public string CloudV5Bespoke { get; }
        // Cloud V5 FODiD license
        public string CloudV5FoDidLicense { get; }

        // Environment variable name for test configuration file path
        private const string _testConfigFileVarName = "TEST_CONFIG_FILE";

        // setting name
        private const string _rootUrl = "cloud_root_url"; // Root URL for test web request
        private const string _freeResourceKey = "free_resource_key"; // Free resource key no properties
        private const string _freeResourceKeyProps = "free_resource_key_props"; // Free resource key with properties
        private const string _paidResourceKey = "paid_resource_key"; // Paid resource key no properties
        private const string _paidResourceKeyProps = "paid_resource_key_props"; // Paid resource key with properties
        private const string _expiredResourceKey = "expired_resource_key"; // Resource key whose subscription has expired
        private const string _zeroQuotaResourceKey = "zero_quota_resource_key"; // Resource key with zero remaining quota
        private const string _restrictedResourceKey = "restricted_resource_key"; // Resource key manually restricted
        private const string _domainRestrictedResourceKey = "domain_restricted_resource_key"; // Resource key restricted to a specific referer domain
        private const string _fodidResourceKey = "fodid_resource_key"; // FODiD-enabled resource key
        private const string _freeLicense = "free_license"; // Free V1 license
        private const string _premiumLicense = "premium_license"; // Premium V1 license
        private const string _freeV4License = "free_v4_license"; // Free V4 license
        private const string _enterpriseV4License = "enterprise_v4_license"; // Enterprise V4 license
        private const string _cloudV5Bespoke = "cloud_v5_bespoke"; // Cloud V5 Bespoke
        private const string _cloudV5FoDidLicense = "cloud_v5_fodid_license"; // Cloud V5 FODiD license

        // Logger
        private readonly ILogger<TestConfig> _logger;
        // lock object
        private static readonly object _syncLock = new object();
        // Singleton
        private static TestConfig _instance = null;
        private TestConfig(ILogger<TestConfig> logger) {
            // Set logger
            _logger = logger;
            // Check if a custome config exist.
            string configFile = Environment.GetEnvironmentVariable(_testConfigFileVarName);
            _logger.LogInformation($"Process test configuration file '{configFile}'");
            // Check if config file exist
            if (configFile == null)
            {
                _logger.LogInformation("No configuration file specified. Use defaults.");
                RootUrl = "";
                FreeResourceKey = "AQRbfSjrDfRfZRDC2Eg";
                FreeResourceKeyProps = "AQRbfSjrUzHYkQPG2Eg";
                PaidResourceKey = "AQRbfSjrcS0fcBDC2Eg";
                PaidResourceKeyProps = "AQRbfSjrftF1pwPG2Eg";
                ExpiredResourceKey = "expired-resource-key";
                ZeroQuotaResourceKey = "zero-quota-resource-key";
                RestrictedResourceKey = "restricted-resource-key";
                DomainRestrictedResourceKey = "AQQNX4o8w56YjUAy2Eg";
                FodidResourceKey = "BAR_AAABQFWniqS23kg";
                FreeLicense = "CloudFree";
                PremiumLicense = "CloudPremium";
                FreeV4License = "CloudV4Free";
                EnterpriseV4License = "CloudV4Complete";
                CloudV5Bespoke = "CloudV5Bespoke";
                CloudV5FoDidLicense = "CloudV5FoDid";
            }
            else if (File.Exists(configFile))
            {
                using (StreamReader file = new StreamReader(configFile))
                {
                    // Throw exception if error occurs
                    string json = file.ReadToEnd();
                    // Parse the content
                    JObject config = JObject.Parse(json);
                    // Set root URL
                    RootUrl = (string)config[_rootUrl];
                    _logger.LogInformation($"{_rootUrl}: {RootUrl}");

                    // Set free resource key
                    FreeResourceKey = (string)config[_freeResourceKey];
                    _logger.LogInformation($"{_freeResourceKey}: {FreeResourceKey}");

                    // Set free resource key with properties
                    FreeResourceKeyProps = (string)config[_freeResourceKeyProps];
                    _logger.LogInformation($"{_freeResourceKeyProps}: {FreeResourceKeyProps}");

                    // Set paid resource key
                    PaidResourceKey = (string)config[_paidResourceKey];
                    _logger.LogInformation($"{_paidResourceKey}: {PaidResourceKey}");

                    // Set paid resource key with properties
                    PaidResourceKeyProps = (string)config[_paidResourceKeyProps];
                    _logger.LogInformation($"{_paidResourceKeyProps}: {PaidResourceKeyProps}");

                    // Set expired resource key
                    ExpiredResourceKey = (string)config[_expiredResourceKey];
                    _logger.LogInformation($"{_expiredResourceKey}: {ExpiredResourceKey}");

                    // Set zero-quota resource key
                    ZeroQuotaResourceKey = (string)config[_zeroQuotaResourceKey];
                    _logger.LogInformation($"{_zeroQuotaResourceKey}: {ZeroQuotaResourceKey}");

                    // Set restricted resource key
                    RestrictedResourceKey = (string)config[_restrictedResourceKey];
                    _logger.LogInformation($"{_restrictedResourceKey}: {RestrictedResourceKey}");

                    // Set domain-restricted resource key
                    DomainRestrictedResourceKey = (string)config[_domainRestrictedResourceKey];
                    _logger.LogInformation($"{_domainRestrictedResourceKey}: {DomainRestrictedResourceKey}");

                    // Set FODiD-enabled resource key
                    FodidResourceKey = (string)config[_fodidResourceKey];
                    _logger.LogInformation($"{_fodidResourceKey}: {FodidResourceKey}");

                    // Set free license
                    FreeLicense = (string)config[_freeLicense];
                    _logger.LogInformation($"{_freeLicense}: {FreeLicense}");

                    // Set premium license
                    PremiumLicense = (string)config[_premiumLicense];
                    _logger.LogInformation($"{_premiumLicense}: {PremiumLicense}");

                    // Set free v4 license
                    FreeV4License = (string)config[_freeV4License];
                    _logger.LogInformation($"{_freeV4License}: {FreeV4License}");

                    // Set enterprise v4 license
                    EnterpriseV4License = (string)config[_enterpriseV4License];
                    _logger.LogInformation($"{_enterpriseV4License}: {EnterpriseV4License}");

                    // Set cloud v5 bespoke
                    CloudV5Bespoke = (string)config[_cloudV5Bespoke];
                    _logger.LogInformation($"{_cloudV5Bespoke}: {CloudV5Bespoke}");

                    // Set CloudV5FODiD synthetic license token
                    CloudV5FoDidLicense = (string)config[_cloudV5FoDidLicense];
                    _logger.LogInformation($"{_cloudV5FoDidLicense}: {CloudV5FoDidLicense}");
                }
            }
            else
            {
                throw new FileNotFoundException($"Test configuration file '{configFile}' does not exist.");
            }
        }

        public static TestConfig Instance() {
            // Make sure we don't create multi objects if invoked by different threads.
            if (_instance == null)
            {
                // Lock here so that if _instance is already created,
                // other threads won't have to wait
                lock (_syncLock)
                {
                    // Check _instance again to make sure it is yet created
                    // given that the lock is now acquired.
                    if (_instance == null)
                    {
                        _instance = new TestConfig(new TestLoggerFactory().CreateLogger<TestConfig>());
                    }
                }
            }
            return _instance;
        }
    }
}
