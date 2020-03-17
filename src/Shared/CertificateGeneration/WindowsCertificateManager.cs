using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.AspNetCore.Certificates.Generation
{
    internal class WindowsCertificateManager : CertificateManager
    {
        private const int UserCancelledErrorCode = 1223;

        public override bool IsExportable(X509Certificate2 c)
        {
#if !XPLAT
            return (c.GetRSAPrivateKey() is RSACryptoServiceProvider rsaPrivateKey &&
                    rsaPrivateKey.CspKeyContainerInfo.Exportable) ||
                (c.GetRSAPrivateKey() is RSACng cngPrivateKey &&
                    cngPrivateKey.Key.ExportPolicy == CngExportPolicies.AllowExport);
#else
            // Only check for RSA CryptoServiceProvider and do not fail in XPlat tooling as
            // System.Security.Cryptography.Cng is not part of the shared framework and we don't
            // want to bring the dependency in on CLI scenarios. This functionality will be used
            // on CLI scenarios as part of the first run experience, so checking the exportability
            // of the certificate is not important.
            return
                ((c.GetRSAPrivateKey() is RSACryptoServiceProvider rsaPrivateKey &&
                    rsaPrivateKey.CspKeyContainerInfo.Exportable) || !(c.GetRSAPrivateKey() is RSACryptoServiceProvider));
#endif
        }

        public override X509Certificate2 SaveCertificateInStore(X509Certificate2 certificate, StoreName name, StoreLocation location)
        {
            // On non OSX systems we need to export the certificate and import it so that the transient
            // key that we generated gets persisted.
            var export = certificate.Export(X509ContentType.Pkcs12, "");
            certificate = new X509Certificate2(export, "", X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
            Array.Clear(export, 0, export.Length);
            certificate.FriendlyName = certificate.FriendlyName;

            return base.SaveCertificateInStore(certificate, name, location);
        }

        internal override void TrustCertificateCore(X509Certificate2 certificate)
        {
            var publicCertificate = new X509Certificate2(certificate.Export(X509ContentType.Cert));

            publicCertificate.FriendlyName = certificate.FriendlyName;

            using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);

            store.Open(OpenFlags.ReadWrite);
            var existing = store.Certificates.Find(X509FindType.FindByThumbprint, publicCertificate.Thumbprint, validOnly: false);
            if (existing.Count > 0)
            {
                Log.WindowsCertificateAlreadyTrusted();
                DisposeCertificates(existing.OfType<X509Certificate2>());
                return;
            }

            try
            {
                Log.WindowsAddCertificateToRootStore();
                store.Add(publicCertificate);
                store.Close();
            }
            catch (CryptographicException exception) when (exception.HResult == UserCancelledErrorCode)
            {
                Log.WindowsCertificateTrustCanceled();
                throw new UserCancelledTrustException();
            }
        }

        internal override void RemoveCertificateFromTrustedRoots(X509Certificate2 certificate)
        {
            Log.WindowsRemoveCertificateFromRootStoreStart();
            using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);

            store.Open(OpenFlags.ReadWrite);
            var matching = store.Certificates
                .OfType<X509Certificate2>()
                .SingleOrDefault(c => c.SerialNumber == certificate.SerialNumber);

            if (matching != null)
            {
                store.Remove(matching);
            }
            else
            {
                Log.WindowsRemoveCertificateFromRootStoreNotFound();
            }

            store.Close();
            Log.WindowsRemoveCertificateFromRootStoreEnd();
        }

        public override bool IsTrusted(X509Certificate2 certificate)
        {
            return ListCertificates(StoreName.Root, StoreLocation.CurrentUser, isValid: true, requireExportable: false)
                .Any(c => c.Thumbprint == certificate.Thumbprint);
        }

        internal override IList<X509Certificate2> GetCertificatesToRemove(StoreName storeName, StoreLocation storeLocation)
        {
            return ListCertificates(storeName, storeLocation, isValid: false);
        }
    }
}
