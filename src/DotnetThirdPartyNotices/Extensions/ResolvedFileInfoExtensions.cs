using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DotnetThirdPartyNotices.LicenseResolvers;
using DotnetThirdPartyNotices.Models;

namespace DotnetThirdPartyNotices.Extensions
{
    internal static class ResolvedFileInfoExtensions
    {
        private static readonly Lazy<List<ILicenseUriLicenseResolver>> LicenseUriLicenseResolvers =
            new Lazy<List<ILicenseUriLicenseResolver>>(() =>
                GetInstancesFromExecutingAssembly<ILicenseUriLicenseResolver>().ToList());

        private static readonly Lazy<List<IProjectUriLicenseResolver>> ProjectUriLicenseResolvers =
            new Lazy<List<IProjectUriLicenseResolver>>(() =>
                GetInstancesFromExecutingAssembly<IProjectUriLicenseResolver>().ToList());

        private static readonly Lazy<List<IFileVersionInfoLicenseResolver>> FileVersionInfoLicenseResolvers =
            new Lazy<List<IFileVersionInfoLicenseResolver>>(() =>
                GetInstancesFromExecutingAssembly<IFileVersionInfoLicenseResolver>().ToList());

        private static IEnumerable<T> GetInstancesFromExecutingAssembly<T>() where T : class
        {
            return Assembly.GetExecutingAssembly().GetInstances<T>();
        }

        private static bool TryFindLicenseUriLicenseResolver(Uri licenseUri, out ILicenseUriLicenseResolver resolver)
        {
            resolver = LicenseUriLicenseResolvers.Value.Find(r => r.CanResolve(licenseUri));
            return resolver != null;
        }

        private static bool TryFindProjectUriLicenseResolver(Uri projectUri, out IProjectUriLicenseResolver resolver)
        {
            resolver = ProjectUriLicenseResolvers.Value.Find(r => r.CanResolve(projectUri));
            return resolver != null;
        }

        private static bool TryFindFileVersionInfoLicenseResolver(
            FileVersionInfo fileVersionInfo, out IFileVersionInfoLicenseResolver resolver)
        {
            resolver = FileVersionInfoLicenseResolvers.Value.Find(r => r.CanResolve(fileVersionInfo));
            return resolver != null;
        }

        public static async Task<string> ResolveLicense(this ResolvedFileInfo resolvedFileInfo)
        {
            if (resolvedFileInfo == null) throw new ArgumentNullException(nameof(resolvedFileInfo));
            string license = null;
            if (resolvedFileInfo.NuSpec != null)
                license = await ResolveLicense(resolvedFileInfo.NuSpec);

            return license ?? await ResolveLicenseFromFileVersionInfo(resolvedFileInfo.VersionInfo);
        }
        
        private static async Task<string> ResolveLicense(NuSpec nuSpec)
        {
            // Try to get the license from license url
            if (!string.IsNullOrEmpty(nuSpec.LicenseUrl))
            {
                var licenseUri = new Uri(nuSpec.LicenseUrl);
                var license = await ResolveLicenseFromLicenseUri(licenseUri);
                if (license != null)
                    return license;
            }

            // Otherwise try to get the license from project url
            if (string.IsNullOrEmpty(nuSpec.ProjectUrl)) return null;

            var projectUri = new Uri(nuSpec.ProjectUrl);
            return await ResolveLicenseFromProjectUri(projectUri);
        }

        private static async Task<string> ResolveLicenseFromLicenseUri(Uri licenseUri)
        {
            if (TryFindLicenseUriLicenseResolver(licenseUri, out var licenseUriLicenseResolver))
                return await licenseUriLicenseResolver.Resolve(licenseUri);

            // TODO: redirect uris should be checked at the very end to save us from redundant requests (when no resolver for anything can be found)
            var redirectUri = await licenseUri.GetRedirectUri();
            if (redirectUri != null)
                return await ResolveLicenseFromLicenseUri(redirectUri);

            // Finally, if no license uri can be found despite all the redirects, try to blindly get it
            return await licenseUri.GetPlainText();
        }

        private static async Task<string> ResolveLicenseFromProjectUri(Uri projectUri)
        {
            if (TryFindProjectUriLicenseResolver(projectUri, out var projectUriLicenseResolver))
                return await projectUriLicenseResolver.Resolve(projectUri);

            // TODO: redirect uris should be checked at the very end to save us from redundant requests (when no resolver for anything can be found)
            var redirectUri = await projectUri.GetRedirectUri();
            if (redirectUri != null)
                return await ResolveLicenseFromProjectUri(redirectUri);

            return null;
        }

        private static Task<string> ResolveLicenseFromFileVersionInfo(FileVersionInfo fileVersionInfo)
        {
            if (!TryFindFileVersionInfoLicenseResolver(fileVersionInfo, out var fileVersionInfoLicenseResolver))
                return null;

            return fileVersionInfoLicenseResolver.Resolve(fileVersionInfo);
        }
    }
}