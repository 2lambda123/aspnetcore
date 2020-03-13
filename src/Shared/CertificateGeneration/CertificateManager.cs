// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Microsoft.AspNetCore.Certificates.Generation
{
    internal class CertificateManager
    {
        public const string AspNetHttpsOid = "1.3.6.1.4.1.311.84.1.1";
        public const string AspNetHttpsOidFriendlyName = "ASP.NET Core HTTPS development certificate";

        private const string ServerAuthenticationEnhancedKeyUsageOid = "1.3.6.1.5.5.7.3.1";
        private const string ServerAuthenticationEnhancedKeyUsageOidFriendlyName = "Server Authentication";

        internal const string LocalhostHttpsDnsName = "localhost";
        internal const string LocalhostHttpsDistinguishedName = "CN=" + LocalhostHttpsDnsName;

        public const int RSAMinimumKeySizeInBits = 2048;

        public static CertificateManager Instance { get; } = new CertificateManager();

        // Setting to 0 means we don't append the version byte,
        // which is what all machines currently have.
        public static int AspNetHttpsCertificateVersion { get; set; } = 1;

        public bool IsHttpsDevelopmentCertificate(X509Certificate2 certificate) =>
            certificate.Extensions.OfType<X509Extension>()
            .Any(e => string.Equals(AspNetHttpsOid, e.Oid.Value, StringComparison.Ordinal));

        public IList<X509Certificate2> ListCertificates(
            StoreName storeName,
            StoreLocation location,
            bool isValid,
            bool requireExportable = true,
            DiagnosticInformation diagnostics = null)
        {
            diagnostics?.Debug($"Listing 'HTTPS' certificates on '{location}\\{storeName}'.");
            var certificates = new List<X509Certificate2>();
            try
            {
                using (var store = new X509Store(storeName, location))
                {
                    store.Open(OpenFlags.ReadOnly);
                    certificates.AddRange(store.Certificates.OfType<X509Certificate2>());
                    IEnumerable<X509Certificate2> matchingCertificates = certificates;
                    matchingCertificates = matchingCertificates
                        .Where(c => HasOid(c, AspNetHttpsOid));

                    diagnostics?.Debug(diagnostics.DescribeCertificates(matchingCertificates));
                    if (isValid)
                    {
                        // Ensure the certificate hasn't expired, has a private key and its exportable
                        // (for container/unix scenarios).
                        diagnostics?.Debug("Checking certificates for validity.");
                        var now = DateTimeOffset.Now;
                        var validCertificates = matchingCertificates
                            .Where(c => c.NotBefore <= now &&
                                now <= c.NotAfter &&
                                (!requireExportable || IsExportable(c))
                                && MatchesVersion(c))
                            .ToArray();

                        var invalidCertificates = matchingCertificates.Except(validCertificates);

                        diagnostics?.Debug("Listing valid certificates");
                        diagnostics?.Debug(diagnostics.DescribeCertificates(validCertificates));
                        diagnostics?.Debug("Listing invalid certificates");
                        diagnostics?.Debug(diagnostics.DescribeCertificates(invalidCertificates));

                        matchingCertificates = validCertificates;
                    }

                    // We need to enumerate the certificates early to prevent dispoisng issues.
                    matchingCertificates = matchingCertificates.ToList();

                    var certificatesToDispose = certificates.Except(matchingCertificates);
                    DisposeCertificates(certificatesToDispose);

                    store.Close();

                    return (IList<X509Certificate2>)matchingCertificates;
                }
            }
            catch
            {
                DisposeCertificates(certificates);
                certificates.Clear();
                return certificates;
            }

            bool HasOid(X509Certificate2 certificate, string oid) =>
                certificate.Extensions.OfType<X509Extension>()
                    .Any(e => string.Equals(oid, e.Oid.Value, StringComparison.Ordinal));

            static bool MatchesVersion(X509Certificate2 c)
            {
                var byteArray = c.Extensions.OfType<X509Extension>()
                    .Where(e => string.Equals(AspNetHttpsOid, e.Oid.Value, StringComparison.Ordinal))
                    .Single()
                    .RawData;

                if ((byteArray.Length == AspNetHttpsOidFriendlyName.Length && byteArray[0] == (byte)'A') || byteArray.Length == 0)
                {
                    // No Version set, default to 0
                    return 0 >= AspNetHttpsCertificateVersion;
                }
                else
                {
                    // Version is in the only byte of the byte array.
                    return byteArray[0] >= AspNetHttpsCertificateVersion;
                }
            }
        }

        public virtual bool IsExportable(X509Certificate2 c) => false;

        internal static void DisposeCertificates(IEnumerable<X509Certificate2> disposables)
        {
            foreach (var disposable in disposables)
            {
                try
                {
                    disposable.Dispose();
                }
                catch
                {
                }
            }
        }

        public IList<X509Certificate2> GetHttpsCertificates() =>
            ListCertificates(StoreName.My, StoreLocation.CurrentUser, isValid: true, requireExportable: true);

        public X509Certificate2 CreateAspNetCoreHttpsDevelopmentCertificate(DateTimeOffset notBefore, DateTimeOffset notAfter, string subjectOverride, DiagnosticInformation diagnostics = null)
        {
            var subject = new X500DistinguishedName(subjectOverride ?? LocalhostHttpsDistinguishedName);
            var extensions = new List<X509Extension>();
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName(LocalhostHttpsDnsName);

            var keyUsage = new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, critical: true);
            var enhancedKeyUsage = new X509EnhancedKeyUsageExtension(
                new OidCollection() {
                    new Oid(
                        ServerAuthenticationEnhancedKeyUsageOid,
                        ServerAuthenticationEnhancedKeyUsageOidFriendlyName)
                },
                critical: true);

            var basicConstraints = new X509BasicConstraintsExtension(
                certificateAuthority: false,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true);

            byte[] bytePayload;

            if (AspNetHttpsCertificateVersion != 0)
            {
                bytePayload = new byte[1];
                bytePayload[0] = (byte)AspNetHttpsCertificateVersion;
            }
            else
            {
                bytePayload = Encoding.ASCII.GetBytes(AspNetHttpsOidFriendlyName);
            }

            var aspNetHttpsExtension = new X509Extension(
                new AsnEncodedData(
                    new Oid(AspNetHttpsOid, AspNetHttpsOidFriendlyName),
                    bytePayload),
                critical: false);

            extensions.Add(basicConstraints);
            extensions.Add(keyUsage);
            extensions.Add(enhancedKeyUsage);
            extensions.Add(sanBuilder.Build(critical: true));
            extensions.Add(aspNetHttpsExtension);

            var certificate = CreateSelfSignedCertificate(subject, extensions, notBefore, notAfter);
            return certificate;
        }

        internal static bool CheckDeveloperCertificateKey(X509Certificate2 candidate)
        {
            // Tries to use the certificate key to validate it can't access it
            try
            {
                var rsa = candidate.GetRSAPrivateKey();
                if (rsa == null)
                {
                    return false;
                }

                // Encrypting a random value is the ultimate test for a key validity.
                // Windows and Mac OS both return HasPrivateKey = true if there is (or there has been) a private key associated
                // with the certificate at some point.
                var value = new byte[32];
                RandomNumberGenerator.Fill(value);
                rsa.Decrypt(rsa.Encrypt(value, RSAEncryptionPadding.Pkcs1), RSAEncryptionPadding.Pkcs1);

                // Being able to encrypt and decrypt a payload is the strongest guarantee that the key is valid.
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public virtual X509Certificate2 CreateSelfSignedCertificate(
            X500DistinguishedName subject,
            IEnumerable<X509Extension> extensions,
            DateTimeOffset notBefore,
            DateTimeOffset notAfter)
        {
            var key = CreateKeyMaterial(RSAMinimumKeySizeInBits);

            var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            foreach (var extension in extensions)
            {
                request.CertificateExtensions.Add(extension);
            }

            var result = request.CreateSelfSigned(notBefore, notAfter);
            result.FriendlyName = AspNetHttpsOidFriendlyName;
            return result;

            RSA CreateKeyMaterial(int minimumKeySize)
            {
                var rsa = RSA.Create(minimumKeySize);
                if (rsa.KeySize < minimumKeySize)
                {
                    throw new InvalidOperationException($"Failed to create a key with a size of {minimumKeySize} bits");
                }

                return rsa;
            }
        }

        public virtual X509Certificate2 SaveCertificateInStore(X509Certificate2 certificate, StoreName name, StoreLocation location, DiagnosticInformation diagnostics = null)
        {
            diagnostics?.Debug("Saving the certificate into the certificate store.");

            using (var store = new X509Store(name, location))
            {
                store.Open(OpenFlags.ReadWrite);
                store.Add(certificate);
                store.Close();
            };

            return certificate;
        }

        public void ExportCertificate(X509Certificate2 certificate, string path, bool includePrivateKey, string password, DiagnosticInformation diagnostics = null)
        {
            diagnostics?.Debug(
                $"Exporting certificate to '{path}'",
                includePrivateKey ? "The certificate will contain the private key" : "The certificate will not contain the private key");
            if (includePrivateKey && password == null)
            {
                diagnostics?.Debug("No password was provided for the certificate.");
            }

            var targetDirectoryPath = Path.GetDirectoryName(path);
            if (targetDirectoryPath != "")
            {
                diagnostics?.Debug($"Ensuring that the directory for the target exported certificate path exists '{targetDirectoryPath}'");
                Directory.CreateDirectory(targetDirectoryPath);
            }

            byte[] bytes;
            if (includePrivateKey)
            {
                try
                {
                    diagnostics?.Debug($"Exporting the certificate including the private key.");
                    bytes = certificate.Export(X509ContentType.Pkcs12, password);
                }
                catch (Exception e)
                {
                    diagnostics?.Error($"Failed to export the certificate with the private key", e);
                    throw;
                }
            }
            else
            {
                try
                {
                    diagnostics?.Debug($"Exporting the certificate without the private key.");
                    bytes = certificate.Export(X509ContentType.Cert);
                }
                catch (Exception ex)
                {
                    diagnostics?.Error($"Failed to export the certificate without the private key", ex);
                    throw;
                }
            }
            try
            {
                diagnostics?.Debug($"Writing exported certificate to path '{path}'.");
                File.WriteAllBytes(path, bytes);
            }
            catch (Exception ex)
            {
                diagnostics?.Error("Failed writing the certificate to the target path", ex);
                throw;
            }
            finally
            {
                Array.Clear(bytes, 0, bytes.Length);
            }
        }

        internal virtual void TrustCertificate(X509Certificate2 certificate, DiagnosticInformation diagnostics = null)
        {
        }

        public virtual bool IsTrusted(X509Certificate2 certificate)
        {
            return false;
        }

        public void CleanupHttpsCertificates(string subject = LocalhostHttpsDistinguishedName)
        {
            CleanupCertificates(subject);
        }

        public void CleanupCertificates(string subject)
        {
            // On OS X we don't have a good way to manage trusted certificates in the system keychain
            // so we do everything by invoking the native toolchain.
            // This has some limitations, like for example not being able to identify our custom OID extension. For that
            // matter, when we are cleaning up certificates on the machine, we start by removing the trusted certificates.
            // To do this, we list the certificates that we can identify on the current user personal store and we invoke
            // the native toolchain to remove them from the sytem keychain. Once we have removed the trusted certificates,
            // we remove the certificates from the local user store to finish up the cleanup.
            var certificates = ListCertificates(StoreName.My, StoreLocation.CurrentUser, isValid: false);
            foreach (var certificate in certificates)
            {
                RemoveCertificate(certificate, RemoveLocations.All);
            }
        }

        public DiagnosticInformation CleanupHttpsCertificates2(string subject = LocalhostHttpsDistinguishedName)
        {
            return CleanupCertificates2(subject);
        }

        public DiagnosticInformation CleanupCertificates2(string subject)
        {
            var diagnostics = new DiagnosticInformation();
            // On OS X we don't have a good way to manage trusted certificates in the system keychain
            // so we do everything by invoking the native toolchain.
            // This has some limitations, like for example not being able to identify our custom OID extension. For that
            // matter, when we are cleaning up certificates on the machine, we start by removing the trusted certificates.
            // To do this, we list the certificates that we can identify on the current user personal store and we invoke
            // the native toolchain to remove them from the sytem keychain. Once we have removed the trusted certificates,
            // we remove the certificates from the local user store to finish up the cleanup.
            var certificates = ListCertificates(StoreName.My, StoreLocation.CurrentUser, isValid: false, requireExportable: true, diagnostics);
            foreach (var certificate in certificates)
            {
                RemoveCertificate(certificate, RemoveLocations.All, diagnostics);
            }

            return diagnostics;
        }

        public void RemoveAllCertificates(StoreName storeName, StoreLocation storeLocation, string subject = null)
        {
            var certificates = GetCertificatesToRemove(storeName, storeLocation);
            var certificatesWithName = subject == null ? certificates : certificates.Where(c => c.Subject == subject);

            var removeLocation = storeName == StoreName.My ? RemoveLocations.Local : RemoveLocations.Trusted;

            foreach (var certificate in certificates)
            {
                RemoveCertificate(certificate, removeLocation);
            }

            DisposeCertificates(certificates);
        }

        internal virtual IList<X509Certificate2> GetCertificatesToRemove(StoreName storeName, StoreLocation storeLocation)
        {
            return ListCertificates(storeName, storeLocation, isValid: false);
        }

        internal virtual void RemoveCertificate(X509Certificate2 certificate, RemoveLocations locations, DiagnosticInformation diagnostics = null)
        {
            switch (locations)
            {
                case RemoveLocations.Undefined:
                    throw new InvalidOperationException($"'{nameof(RemoveLocations.Undefined)}' is not a valid location.");
                case RemoveLocations.Local:
                    RemoveCertificateFromUserStore(certificate, diagnostics);
                    break;
                case RemoveLocations.Trusted:
                    RemoveCertificateFromTrustedRoots(certificate, diagnostics);
                    break;
                case RemoveLocations.All:
                    RemoveCertificateFromTrustedRoots(certificate, diagnostics);
                    RemoveCertificateFromUserStore(certificate, diagnostics);
                    break;
                default:
                    throw new InvalidOperationException("Invalid location.");
            }
        }

        private static void RemoveCertificateFromUserStore(X509Certificate2 certificate, DiagnosticInformation diagnostics)
        {
            diagnostics?.Debug($"Trying to remove certificate with thumbprint '{certificate.Thumbprint}' from certificate store '{StoreLocation.CurrentUser}\\{StoreName.My}'.");
            using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadWrite);
                var matching = store.Certificates
                    .OfType<X509Certificate2>()
                    .Single(c => c.SerialNumber == certificate.SerialNumber);

                store.Remove(matching);
                store.Close();
            }
        }

        internal virtual void RemoveCertificateFromTrustedRoots(X509Certificate2 certificate, DiagnosticInformation diagnostics)
        {
        }

        public DetailedEnsureCertificateResult EnsureAspNetCoreHttpsDevelopmentCertificate(
            DateTimeOffset notBefore,
            DateTimeOffset notAfter,
            string path = null,
            bool trust = false,
            bool includePrivateKey = false,
            string password = null,
            string subject = LocalhostHttpsDistinguishedName,
            bool isInteractive = true)
        {
            return EnsureValidCertificateExists(notBefore, notAfter, path, trust, includePrivateKey, password, subject, isInteractive);
        }

        public DetailedEnsureCertificateResult EnsureValidCertificateExists(
            DateTimeOffset notBefore,
            DateTimeOffset notAfter,
            string path = null,
            bool trust = false,
            bool includePrivateKey = false,
            string password = null,
            string subjectOverride = null,
            bool isInteractive = true)
        {
            var result = new DetailedEnsureCertificateResult();

            var certificates = ListCertificates(StoreName.My, StoreLocation.CurrentUser, isValid: true, requireExportable: true, result.Diagnostics).Concat(
                ListCertificates(StoreName.My, StoreLocation.LocalMachine, isValid: true, requireExportable: true, result.Diagnostics));

            var filteredCertificates = subjectOverride == null ? certificates : certificates.Where(c => c.Subject == subjectOverride);
            if (subjectOverride != null)
            {
                var excludedCertificates = certificates.Except(filteredCertificates);

                result.Diagnostics.Debug($"Filtering found certificates to those with a subject equal to '{subjectOverride}'");
                result.Diagnostics.Debug(result.Diagnostics.DescribeCertificates(filteredCertificates));
                result.Diagnostics.Debug($"Listing certificates excluded from consideration.");
                result.Diagnostics.Debug(result.Diagnostics.DescribeCertificates(excludedCertificates));
            }
            else
            {
                result.Diagnostics.Debug("Skipped filtering certificates by subject.");
            }

            if (!TryEnsureCertificatesAreAccessibleAcrossPartitions(filteredCertificates, isInteractive, result))
            {
                return result;
            }

            certificates = filteredCertificates;

            result.ResultCode = EnsureCertificateResult.Succeeded;

            X509Certificate2 certificate = null;
            if (certificates.Count() > 0)
            {
                result.Diagnostics.Debug("Found valid certificates present on the machine.");
                result.Diagnostics.Debug(result.Diagnostics.DescribeCertificates(certificates));
                certificate = certificates.First();
                result.Diagnostics.Debug("Selected certificate");
                result.Diagnostics.Debug(result.Diagnostics.DescribeCertificates(certificate));
                result.ResultCode = EnsureCertificateResult.ValidCertificatePresent;
            }
            else
            {
                result.Diagnostics.Debug("No valid certificates present on this machine. Trying to create one.");
                try
                {
                    certificate = CreateAspNetCoreHttpsDevelopmentCertificate(notBefore, notAfter, subjectOverride, result.Diagnostics);
                }
                catch (Exception e)
                {
                    result.Diagnostics.Error("Error creating the certificate.", e);
                    result.ResultCode = EnsureCertificateResult.ErrorCreatingTheCertificate;
                    return result;
                }

                try
                {
                    certificate = SaveCertificateInStore(certificate, StoreName.My, StoreLocation.CurrentUser, result.Diagnostics);
                }
                catch (Exception e)
                {
                    result.Diagnostics.Error($"Error saving the certificate in the certificate store '{StoreLocation.CurrentUser}\\{StoreName.My}'.", e);
                    result.ResultCode = EnsureCertificateResult.ErrorSavingTheCertificateIntoTheCurrentUserPersonalStore;
                    return result;
                }
            }
            if (path != null)
            {
                result.Diagnostics.Debug("Trying to export the certificate.");
                result.Diagnostics.Debug(result.Diagnostics.DescribeCertificates(certificate));
                try
                {
                    ExportCertificate(certificate, path, includePrivateKey, password, result.Diagnostics);
                }
                catch (Exception e)
                {
                    result.Diagnostics.Error("An error ocurred exporting the certificate.", e);
                    result.ResultCode = EnsureCertificateResult.ErrorExportingTheCertificate;
                    return result;
                }
            }

            if (trust)
            {
                try
                {
                    result.Diagnostics.Debug("Trying to export the certificate.");
                    TrustCertificate(certificate, result.Diagnostics);
                }
                catch (UserCancelledTrustException)
                {
                    result.Diagnostics.Error("The user cancelled trusting the certificate.", null);
                    result.ResultCode = EnsureCertificateResult.UserCancelledTrustStep;
                    return result;
                }
                catch (Exception e)
                {
                    result.Diagnostics.Error("There was an error trusting the certificate.", e);
                    result.ResultCode = EnsureCertificateResult.FailedToTrustTheCertificate;
                    return result;
                }
            }

            return result;
        }

        public virtual bool TryEnsureCertificatesAreAccessibleAcrossPartitions(
                        IEnumerable<X509Certificate2> certificates,
            bool isInteractive,
            DetailedEnsureCertificateResult result)
        {
            return true;
        }

        public virtual void MakeCertificateKeyAccessibleAcrossPartitions(X509Certificate2 certificate)
        {
        }

        public virtual bool HasValidCertificateWithInnaccessibleKeyAcrossPartitions() => false;

        internal class UserCancelledTrustException : Exception
        {
        }

        internal enum RemoveLocations
        {
            Undefined,
            Local,
            Trusted,
            All
        }

        internal class DetailedEnsureCertificateResult
        {
            public EnsureCertificateResult ResultCode { get; set; }
            public DiagnosticInformation Diagnostics { get; set; } = new DiagnosticInformation();
        }

        internal class DiagnosticInformation
        {
            public IList<string> Messages { get; } = new List<string>();

            public IList<Exception> Exceptions { get; } = new List<Exception>();

            internal void Debug(params string[] messages)
            {
                foreach (var message in messages)
                {
                    Messages.Add(message);
                }
            }

            internal string[] DescribeCertificates(params X509Certificate2[] certificates)
            {
                return DescribeCertificates(certificates.AsEnumerable());
            }

            internal string[] DescribeCertificates(IEnumerable<X509Certificate2> certificates)
            {
                var result = new List<string>();
                result.Add($"'{certificates.Count()}' found matching the criteria.");
                result.Add($"SUBJECT - THUMBPRINT - NOT BEFORE - EXPIRES - HAS PRIVATE KEY");
                foreach (var certificate in certificates)
                {
                    result.Add(DescribeCertificate(certificate));
                }

                return result.ToArray();
            }

            private static string DescribeCertificate(X509Certificate2 certificate) =>
                $"{certificate.Subject} - {certificate.Thumbprint} - {certificate.NotBefore} - {certificate.NotAfter} - {certificate.HasPrivateKey}";

            internal void Error(string preamble, Exception e)
            {
                Messages.Add(preamble);
                if (Exceptions.Count > 0 && Exceptions[Exceptions.Count - 1] == e)
                {
                    return;
                }

                var ex = e;
                while (ex != null)
                {
                    Messages.Add("Exception message: " + ex.Message);
                    ex = ex.InnerException;
                }

            }
        }
    }
}
