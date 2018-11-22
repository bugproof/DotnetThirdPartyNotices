using System;
using System.Threading.Tasks;

namespace DotnetThirdPartyNotices.LicenseResolvers
{
    internal interface IProjectUriLicenseResolver : ILicenseResolver
    {
        bool CanResolve(Uri projectUri);
        Task<string> Resolve(Uri projectUri);
    }
}