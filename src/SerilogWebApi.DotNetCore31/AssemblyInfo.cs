using System.Reflection;

namespace SerilogWebApi.DotNetCore31
{
    public class AssemblyInfo
    {
        public static Assembly Assembly => typeof(AssemblyInfo).Assembly;

        public static string AssemblyName => typeof(AssemblyInfo).Assembly.GetName().Name;

        public static string Version => Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
    }
}
