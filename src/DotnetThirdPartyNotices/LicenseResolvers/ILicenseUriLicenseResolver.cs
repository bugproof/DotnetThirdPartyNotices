using System;
using System.Threading.Tasks;

namespace DotnetThirdPartyNotices.LicenseResolvers
{
    internal interface ILicenseUriLicenseResolver : ILicenseResolver
    {
        bool CanResolve(Uri licenseUri);
        Task<string> Resolve(Uri licenseUri);
    }
}