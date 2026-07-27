namespace ReToolbox.Utils
{
    // Central fail-closed switches for administrator-level supply-chain boundaries.
    // These must remain false until the corresponding artifacts are pinned by an
    // independently verified digest or trusted publisher signature.
    public static class SecurityPolicy
    {
        public static bool AllowRemoteActivationScripts => false;

        public static bool AllowUnverifiedDirectInstallers => false;

        public static bool AllowUnverifiedAdministratorTools => false;
    }
}
