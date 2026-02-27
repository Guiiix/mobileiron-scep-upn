using Microsoft.Win32;

namespace DMIPatchSCEP
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected static string[] GetProfiles()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\ProfileList"))
            {
                return key?.GetSubKeyNames() ?? Array.Empty<string>();
            }
        }

        protected void CheckForSAN(RegistryKey mobileIronSCEPKey, string profileId)
        {
            foreach (string subKeyName in mobileIronSCEPKey.GetSubKeyNames())
            {

                string SCEPInstallKeyPath = $@"{profileId}\Software\Microsoft\SCEP\MobileIron\{subKeyName}\Install";
                using (RegistryKey SCEPInstallKey = Registry.Users.OpenSubKey(SCEPInstallKeyPath, writable: true))
                {
                    if (SCEPInstallKey != null)
                    {
                        /* Okay, we found the SCEP 'Install' key, let's check SAN value */
                        _logger.LogDebug("Found install key at {SCEPInstallKey}", SCEPInstallKey);

                        string SANValue = (string)SCEPInstallKey.GetValue("SubjectAlternativeNames");
                        _logger.LogDebug($"Current SANValue: {SANValue}");

                        if (SANValue != null)
                        {
                            /* We found the SubjectAlternativeNames value ! */
                            if (SANValue.Contains(";1+"))
                            {
                                /* 
                                 * SANValue has not been modified yet, let's patch the SAN type
                                 * 1  => Other Name (NT Principal Name in N-MDM)
                                 * 11 => UPN: this is what we want!
                                 */

                                SCEPInstallKey.SetValue("SubjectAlternativeNames", SANValue.Replace(";1+", ";11+"));
                                _logger.LogInformation($@"SCEP SAN Type patched at {SCEPInstallKeyPath}\SubjectAlternativeNames");
                            }
                        }
                    }
                }
            }
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DMI Neurons MDM SCEP Patcher started");
            while (!stoppingToken.IsCancellationRequested)
            {
                // We need to get every profiles of the machine because SCEP config is in HKCU...
                string[] profiles = GetProfiles();

                foreach (string profileId in profiles)
                {
                    _logger.LogDebug($"Processing profile {profileId}");

                    string registryKey = $@"{profileId}\Software\Microsoft\SCEP\MobileIron";
                    using (RegistryKey key = Registry.Users.OpenSubKey(registryKey))
                    {
                        if (key != null)
                        {
                            _logger.LogDebug($"Found registry key {registryKey}");
                            CheckForSAN(key, profileId);
                        }

                        else
                        {
                            _logger.LogDebug($"Registry key not found: {registryKey}");
                        }
                    }
                }

                // 500ms should be enough to modify registry values between omadmclient and dmcertinst actions
                await Task.Delay(500, stoppingToken);
            }
        }
    }
}
