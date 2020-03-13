
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace Microsoft.AspNetCore.Certificates.Generation
{
    internal class MacOSCertificateManager : CertificateManager
    {
        private const string CertificateSubjectRegex = "CN=(.*[^,]+).*";
        private const string MacOSSystemKeyChain = "/Library/Keychains/System.keychain";
        private static readonly string MacOSUserKeyChain = Environment.GetEnvironmentVariable("HOME") + "/Library/Keychains/login.keychain-db";
        private const string MacOSFindCertificateCommandLine = "security";
        private static readonly string MacOSFindCertificateCommandLineArgumentsFormat = "find-certificate -c {0} -a -Z -p " + MacOSSystemKeyChain;
        private const string MacOSFindCertificateOutputRegex = "SHA-1 hash: ([0-9A-Z]+)";
        private const string MacOSRemoveCertificateTrustCommandLine = "sudo";
        private const string MacOSRemoveCertificateTrustCommandLineArgumentsFormat = "security remove-trusted-cert -d {0}";
        private const string MacOSDeleteCertificateCommandLine = "sudo";
        private const string MacOSDeleteCertificateCommandLineArgumentsFormat = "security delete-certificate -Z {0} {1}";
        private const string MacOSTrustCertificateCommandLine = "sudo";
        private static readonly string MacOSTrustCertificateCommandLineArguments = "security add-trusted-cert -d -r trustRoot -k " + MacOSSystemKeyChain + " ";
        private const string MacOSSetPartitionKeyPermissionsCommandLine = "sudo";
        private static readonly string MacOSSetPartitionKeyPermissionsCommandLineArguments = "security set-key-partition-list -D localhost -S unsigned:,teamid:UBF8T346G9 " + MacOSUserKeyChain;

        private static readonly TimeSpan MaxRegexTimeout = TimeSpan.FromMinutes(1);

        internal override void TrustCertificate(
            X509Certificate2 publicCertificate,
            DiagnosticInformation diagnostics)
        {
            var tmpFile = Path.GetTempFileName();
            try
            {
                ExportCertificate(publicCertificate, tmpFile, includePrivateKey: false, password: null);
                diagnostics?.Debug("Running the trust command on Mac OS");
                using (var process = Process.Start(MacOSTrustCertificateCommandLine, MacOSTrustCertificateCommandLineArguments + tmpFile))
                {
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException("There was an error trusting the certificate.");
                    }
                }
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
            using (var checkTrustProcess = Process.Start(new ProcessStartInfo(
                MacOSFindCertificateCommandLine,
                string.Format(MacOSFindCertificateCommandLineArgumentsFormat, subject))
            {
                RedirectStandardOutput = true
            }))
            {
                var output = checkTrustProcess.StandardOutput.ReadToEnd();
                checkTrustProcess.WaitForExit();
                var matches = Regex.Matches(output, MacOSFindCertificateOutputRegex, RegexOptions.Multiline, MaxRegexTimeout);
                var hashes = matches.OfType<Match>().Select(m => m.Groups[1].Value).ToList();
                return hashes.Any(h => string.Equals(h, certificate.Thumbprint, StringComparison.Ordinal));
            }
        }

        internal override void RemoveCertificateFromTrustedRoots(X509Certificate2 certificate, DiagnosticInformation diagnostics)
        {
            if (IsTrusted(certificate)) // On OSX this check just ensures its on the system keychain
            {
                try
                {
                    diagnostics?.Debug("Trying to remove the certificate trust rule.");
                    RemoveCertificateTrustRule(certificate);
                }
                catch
                {
                    diagnostics?.Debug("Failed to remove the certificate trust rule.");
                    // We don't care if we fail to remove the trust rule if
                    // for some reason the certificate became untrusted.
                    // The delete command will fail if the certificate is
                    // trusted.
                }
                RemoveCertificateFromKeyChain(MacOSSystemKeyChain, certificate);
            }
            else
            {
                diagnostics?.Debug("The certificate was not trusted.");
            }
        }

        private static void RemoveCertificateTrustRule(X509Certificate2 certificate)
        {
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
                using (var process = Process.Start(processInfo))
                {
                    process.WaitForExit();
                }
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

            using (var process = Process.Start(processInfo))
            {
                var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($@"There was an error removing the certificate with thumbprint '{certificate.Thumbprint}'.

{output}");
                }
            }
        }

        public override void MakeCertificateKeyAccessibleAcrossPartitions(X509Certificate2 certificate)
        {
            if (OtherNonAspNetCoreHttpsCertificatesPresent())
            {
                throw new InvalidOperationException("Unable to make HTTPS certificate key trusted across security partitions.");
            }
            using (var process = Process.Start(MacOSSetPartitionKeyPermissionsCommandLine, MacOSSetPartitionKeyPermissionsCommandLineArguments))
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("Error making the key accessible across partitions.");
                }
            }

            var certificateSentinelPath = GetCertificateSentinelPath(certificate);
            File.WriteAllText(certificateSentinelPath, "true");
        }

        private static string GetCertificateSentinelPath(X509Certificate2 certificate) =>
            Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".dotnet", $"certificate.{certificate.GetCertHashString(HashAlgorithmName.SHA256)}.sentinel");

        private bool OtherNonAspNetCoreHttpsCertificatesPresent()
        {
            var certificates = new List<X509Certificate2>();
            try
            {
                using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
                {
                    store.Open(OpenFlags.ReadOnly);
                    certificates.AddRange(store.Certificates.OfType<X509Certificate2>());
                    IEnumerable<X509Certificate2> matchingCertificates = certificates;
                    // Ensure the certificate hasn't expired, has a private key and its exportable
                    // (for container/unix scenarios).
                    var now = DateTimeOffset.Now;
                    matchingCertificates = matchingCertificates
                        .Where(c => c.NotBefore <= now &&
                            now <= c.NotAfter && c.Subject == CertificateManager.LocalhostHttpsDistinguishedName);

                    // We need to enumerate the certificates early to prevent dispoisng issues.
                    matchingCertificates = matchingCertificates.ToList();

                    var certificatesToDispose = certificates.Except(matchingCertificates);
                    DisposeCertificates(certificatesToDispose);

                    store.Close();

                    return matchingCertificates.All(c => !HasOid(c, AspNetHttpsOid));
                }
            }
            catch
            {
                DisposeCertificates(certificates);
                certificates.Clear();
                return true;
            }

            bool HasOid(X509Certificate2 certificate, string oid) =>
                certificate.Extensions.OfType<X509Extension>()
                .Any(e => string.Equals(oid, e.Oid.Value, StringComparison.Ordinal));
        }

        private bool CanAccessCertificateKeyAcrossPartitions(X509Certificate2 certificate)
        {
            var certificateSentinelPath = GetCertificateSentinelPath(certificate);
            return File.Exists(certificateSentinelPath);
        }

        public override bool TryEnsureCertificatesAreAccessibleAcrossPartitions(
            IEnumerable<X509Certificate2> certificates,
            bool isInteractive,
            DetailedEnsureCertificateResult result)
        {
            foreach (var cert in certificates)
            {
                if (!CanAccessCertificateKeyAcrossPartitions(cert))
                {
                    if (!isInteractive)
                    {
                        // If the process is not interactive (first run experience) bail out. We will simply create a certificate
                        // in case there is none or report success during the first run experience.
                        break;
                    }
                    try
                    {
                        // The command we run handles making keys for all localhost certificates accessible across partitions. If it can not run the
                        // command safely (because there are other localhost certificates that were not created by asp.net core, it will throw.
                        MakeCertificateKeyAccessibleAcrossPartitions(cert);
                        break;
                    }
                    catch (Exception ex)
                    {
                        result.Diagnostics.Error("Failed to make certificate key accessible", ex);
                        result.ResultCode = EnsureCertificateResult.FailedToMakeKeyAccessible;
                        return false;
                    }
                }
            }

            return true;
        }

        public override bool HasValidCertificateWithInnaccessibleKeyAcrossPartitions()
        {
            var certificates = GetHttpsCertificates();
            if (certificates.Count == 0)
            {
                return false;
            }

            // We need to check all certificates as a new one might be created that hasn't been correctly setup.
            var result = false;
            foreach (var certificate in certificates)
            {
                result = result || !CanAccessCertificateKeyAcrossPartitions(certificate);
            }

            return result;
        }

        public override bool IsExportable(X509Certificate2 c) => false;

        public override X509Certificate2 SaveCertificateInStore(X509Certificate2 certificate, StoreName name, StoreLocation location, DiagnosticInformation diagnostics = null)
        {
            using (var store = new X509Store(name, location))
            {
                store.Open(OpenFlags.ReadWrite);
                store.Add(certificate);
                store.Close();
            };

            MakeCertificateKeyAccessibleAcrossPartitions(certificate);

            return certificate;
        }

        internal override IList<X509Certificate2> GetCertificatesToRemove(StoreName storeName, StoreLocation storeLocation)
        {
            return ListCertificates(StoreName.My, StoreLocation.CurrentUser, isValid: false);
        }
    }
}
