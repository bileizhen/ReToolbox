using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace ReToolbox.Utils
{
    public static class SecureStagingDirectory
    {
        public static string Create()
        {
            string commonApplicationData = Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData);
            string path = Path.Combine(
                commonApplicationData,
                $"ReToolbox-SecureStaging-{Guid.NewGuid():N}");

            SecurityIdentifier administrators = new SecurityIdentifier(
                WellKnownSidType.BuiltinAdministratorsSid,
                null);
            SecurityIdentifier system = new SecurityIdentifier(
                WellKnownSidType.LocalSystemSid,
                null);
            DirectorySecurity security = new DirectorySecurity();
            security.SetOwner(administrators);
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(CreateFullControlRule(administrators));
            security.AddAccessRule(CreateFullControlRule(system));

            DirectoryInfo directory = new DirectoryInfo(path);
            FileSystemAclExtensions.Create(directory, security);
            return directory.FullName;
        }

        private static FileSystemAccessRule CreateFullControlRule(
            SecurityIdentifier identity)
        {
            return new FileSystemAccessRule(
                identity,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow);
        }
    }
}
