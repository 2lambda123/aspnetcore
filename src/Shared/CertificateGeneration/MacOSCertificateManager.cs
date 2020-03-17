
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace Microsoft.AspNetCore.Certificates.Generation
{
    internal class MacOSCertificateManager : CertificateManager
    {
        private const string CertificateSubjectRegex = "CN=(.*[^,]+).*";
        private const string MacOSSystemKeyChain = "/Library/Keychains/System.keychain";
        private const string MacOSFindCertificateCommandLine = "security";
        private static readonly string MacOSFindCertificateCommandLineArgumentsFormat = "find-certificate -c {0} -a -Z -p " + MacOSSystemKeyChain;
        private const string MacOSFindCertificateOutputRegex = "SHA-1 hash: ([0-9A-Z]+)";
        private const string MacOSRemoveCertificateTrustCommandLine = "sudo";
        private const string MacOSRemoveCertificateTrustCommandLineArgumentsFormat = "security remove-trusted-cert -d {0}";
        private const string MacOSDeleteCertificateCommandLine = "sudo";
        private const string MacOSDeleteCertificateCommandLineArgumentsFormat = "security delete-certificate -Z {0} {1}";
        private const string MacOSTrustCertificateCommandLine = "sudo";
        private static readonly string MacOSTrustCertificateCommandLineArguments = "security add-trusted-cert -d -r trustRoot -k " + MacOSSystemKeyChain + " ";

        private static readonly TimeSpan MaxRegexTimeout = TimeSpan.FromMinutes(1);

        protected override void TrustCertificateCore(X509Certificate2 publicCertificate)
        {
            var tmpFile = Path.GetTempFileName();
            try
            {
                ExportCertificate(publicCertificate, tmpFile, includePrivateKey: false, password: null);
                Log.MacOSTrustCommandStart($"{MacOSTrustCertificateCommandLine} {MacOSTrustCertificateCommandLineArguments}{tmpFile}");
                using (var process = Process.Start(MacOSTrustCertificateCommandLine, MacOSTrustCertificateCommandLineArguments + tmpFile))
                {
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        Log.MacOSTrustCommandError(process.ExitCode);
                        throw new InvalidOperationException("There was an error trusting the certificate.");
                    }
                }
                Log.MacOSTrustCommandEnd();
            }
            finally
            {
                try
                {
                    if (File.Exists(tmpFile))
                    {
                        File.Delete(tmpFile);
                    }
                }
                catch
                {
                    // We don't care if we can't delete the temp file.
                }
            }
        }

        public override bool IsTrusted(X509Certificate2 certificate)
        {
            var subjectMatch = Regex.Match(certificate.Subject, CertificateSubjectRegex, RegexOptions.Singleline, MaxRegexTimeout);
            if (!subjectMatch.Success)
            {
                throw new InvalidOperationException($"Can't determine the subject for the certificate with subject '{certificate.Subject}'.");
            }
            var subject = subjectMatch.Groups[1].Value;
            using var checkTrustProcess = Process.Start(new ProcessStartInfo(
                MacOSFindCertificateCommandLine,
                string.Format(MacOSFindCertificateCommandLineArgumentsFormat, subject))
            {
                RedirectStandardOutput = true
            });
            var output = checkTrustProcess.StandardOutput.ReadToEnd();
            checkTrustProcess.WaitForExit();
            var matches = Regex.Matches(output, MacOSFindCertificateOutputRegex, RegexOptions.Multiline, MaxRegexTimeout);
            var hashes = matches.OfType<Match>().Select(m => m.Groups[1].Value).ToList();
            return hashes.Any(h => string.Equals(h, certificate.Thumbprint, StringComparison.Ordinal));
        }

        protected override void RemoveCertificateFromTrustedRoots(X509Certificate2 certificate)
        {
            if (IsTrusted(certificate)) // On OSX this check just ensures its on the system keychain
            {
                // A trusted certificate in OSX is installed into the system keychain and
                // as a "trust rule" applied to it.
                // To remove the certificate we first need to remove the "trust rule" and then
                // remove the certificate from the keychain.
                // We don't care if we fail to remove the trust rule if
                // for some reason the certificate became untrusted.
                // Trying to remove the certificate from the keychain will fail if the certificate is
                // trusted.
                try
                {
                    RemoveCertificateTrustRule(certificate);
                }
                catch
                {
                }

                RemoveCertificateFromKeyChain(MacOSSystemKeyChain, certificate);
            }
            else
            {
                Log.MacOSCertificateUntrusted(CertificateManagerEventSource.GetDescription(certificate));
            }
        }

        private static void RemoveCertificateTrustRule(X509Certificate2 certificate)
        {
            Log.MacOSRemoveCertificateTrustRuleStart(CertificateManagerEventSource.GetDescription(certificate));
            var certificatePath = Path.GetTempFileName();
            try
            {
                var certBytes = certificate.Export(X509ContentType.Cert);
                File.WriteAllBytes(certificatePath, certBytes);
                var processInfo = new ProcessStartInfo(
                    MacOSRemoveCertificateTrustCommandLine,
                    string.Format(
                        MacOSRemoveCertificateTrustCommandLineArgumentsFormat,
                        certificatePath
                    ));
                using var process = Process.Start(processInfo);
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    Log.MacOSRemoveCertificateTrustRuleError(process.ExitCode);
                }
                Log.MacOSRemoveCertificateTrustRuleEnd();
            }
            finally
            {
                try
                {
                    if (File.Exists(certificatePath))
                    {
                        File.Delete(certificatePath);
                    }
                }
                catch
                {
                    // We don't care about failing to do clean-up on a temp file.
                }
            }
        }

        private static void RemoveCertificateFromKeyChain(string keyChain, X509Certificate2 certificate)
        {
            var processInfo = new ProcessStartInfo(
                MacOSDeleteCertificateCommandLine,
                string.Format(
                    MacOSDeleteCertificateCommandLineArgumentsFormat,
                    certificate.Thumbprint.ToUpperInvariant(),
                    keyChain
                ))
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Log.MacOSRemoveCertificateFromKeyChainStart(keyChain, CertificateManagerEventSource.GetDescription(certificate));
            using (var process = Process.Start(processInfo))
            {
                var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Log.MacOSRemoveCertificateFromKeyChainError(process.ExitCode);
                    throw new InvalidOperationException($@"There was an error removing the certificate with thumbprint '{certificate.Thumbprint}'.

{output}");
                }
            }

            Log.MacOSRemoveCertificateFromKeyChainEnd();
        }

        protected override bool IsExportable(X509Certificate2 c) => true;

        protected override X509Certificate2 SaveCertificateCore(X509Certificate2 certificate)
        {
            var name = StoreName.My;
            var location = StoreLocation.CurrentUser;

            using (var store = new X509Store(name, location))
            {
                store.Open(OpenFlags.ReadWrite);
                store.Add(certificate);
                store.Close();
            };

            return certificate;
        }

        protected override IList<X509Certificate2> GetCertificatesToRemove(StoreName storeName, StoreLocation storeLocation)
        {
            return ListCertificates(StoreName.My, StoreLocation.CurrentUser, isValid: false);
        }
    }
}
