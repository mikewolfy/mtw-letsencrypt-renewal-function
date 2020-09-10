//using System;
//using Microsoft.Azure.WebJobs;
//using Microsoft.Azure.WebJobs.Host;
//using Microsoft.Extensions.Logging;

//namespace LetsEncrypt.Renewal
//{
//    public static class RenewCerts
//    {
//        [FunctionName("RenewCerts")]
//        public static void Run([TimerTrigger("0 0 0 1 * *")]TimerInfo myTimer, ILogger log)
//        {
//            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
//        }
//    }
//}

//#r "Newtonsoft.Json"
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;
using System.Threading;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault.Models;
using System.Threading.Tasks;

namespace LetsEncrypt.Renewal
{
    public static class RenewCerts
    {
        [FunctionName("RenewCerts")]
        public static async Task Run([TimerTrigger("0 0 0 1 * *")] TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {myTimer.ToString()}");

            var functionAppName = ""; //TODO: fill in!
            var keyVaultUrl = ""; //TODO: fill in!
            var userName = $"${functionAppName}";
            var userPwd = await GetSecretFromKeyVault(keyVaultUrl, "userPwd");
            var pfxPassword = await GetSecretFromKeyVault(keyVaultUrl, "pfxPwd");
            var clientSecret = await GetSecretFromKeyVault(keyVaultUrl, "clientSecret");

            var configBody = new Config()
            {
                AzureEnvironment = new AzureEnvironment()
                {
                    WebAppName = "", //TODO: fill in!
                    ClientId = "", //TODO: fill in!
                    ClientSecret = clientSecret,
                    ResourceGroupName = "root", //TODO: fill in!
                    SubscriptionId = "", //TODO: fill in!
                    Tenant = "", //TODO: fill in!
                },
                AcmeConfig = new AcmeConfig()
                {
                    RegistrationEmail = "", //TODO: fill in!
                    Host = "",
                    AlternateNames = new string[] { },
                    RSAKeyLength = 2048,
                    PFXPassword = pfxPassword,
                    UseProduction = true
                },
                CertificateSettings = new CertificateSettings()
                {
                    UseIPBasedSSL = false
                },
                AuthorizationChallengeProviderConfig = new AuthorizationChallengeProviderConfig()
                {
                    DisableWebConfigUpdate = false
                }
            };

            var client = new HttpClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userName}:{userPwd}")));

            configBody.AcmeConfig.Host = "yannickreekmans.be"; //TODO: fill in!
            await CreateCertificate(configBody, functionAppName, client, log);

            configBody.AzureEnvironment.WebAppName = "blog"; //TODO: fill in!
            configBody.AcmeConfig.Host = "blog.yannickreekmans.be"; //TODO: fill in!
            await CreateCertificate(configBody, functionAppName, client, log);
        }

        private static async Task CreateCertificate(dynamic config, string functionAppName, HttpClient client, ILogger log)
        {
            log.LogInformation($"Creating certificate for {config.AcmeConfig.Host}");
            var executionResult = await client.PostAsync($"https://{functionAppName}.scm.azurewebsites.net/letsencrypt/api/certificates/challengeprovider/http/kudu/certificateinstall/azurewebapp?api-version=2017-09-01",
                         new StringContent(JsonConvert.SerializeObject(config), Encoding.UTF8, "application/json"));
            log.LogInformation(await executionResult.Content.ReadAsStringAsync());
            log.LogInformation($"Done creating certificate for {config.AcmeConfig.Host}");
        }

        private static async Task<string> GetSecretFromKeyVault(string keyVaultUrl, string secretName)
        {
            AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider();
            var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));
            SecretBundle secretValue = await keyVaultClient.GetSecretAsync($"{keyVaultUrl}/secrets/{secretName}");
            return secretValue.Value;
        }

        internal class Config
        {
            internal AzureEnvironment AzureEnvironment { get; set; }
            internal AcmeConfig AcmeConfig { get; set; }
            internal CertificateSettings CertificateSettings { get; set; }
            internal AuthorizationChallengeProviderConfig AuthorizationChallengeProviderConfig { get; set; }
        }

        internal class AzureEnvironment
        {
            internal string WebAppName { get; set; }
            internal string ClientId { get; set; }
            internal string ClientSecret { get; set; }
            internal string ResourceGroupName { get; set; }
            internal string SubscriptionId { get; set; }
            internal string Tenant { get; set; }
        }

        internal class AcmeConfig
        {
            internal string RegistrationEmail { get; set; }
            internal string Host { get; set; }
            internal string[] AlternateNames { get; set; }
            internal int RSAKeyLength { get; set; }
            internal string PFXPassword { get; set; }
            internal bool UseProduction { get; set; }
        }

        internal class CertificateSettings
        {
            internal bool UseIPBasedSSL { get; set; }
        }

        internal class AuthorizationChallengeProviderConfig
        {
            internal bool DisableWebConfigUpdate { get; set; }
        }
    }
}