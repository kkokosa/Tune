using System;

namespace Tune.Core.Extensions
{
    public static class DiagnosticAssembyPlatformExtension
    {
        public static string ToPlatformString(this DiagnosticAssembyPlatform platform)
        {
            switch (platform)
            {
                case DiagnosticAssembyPlatform.x86:
                    return "32";
                case DiagnosticAssembyPlatform.x64:
                    return "64";
                default:
                    throw new ArgumentOutOfRangeException("Invalid platform type");
            }
        }
    }

}
