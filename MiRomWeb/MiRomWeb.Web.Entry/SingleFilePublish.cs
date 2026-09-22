using System.Reflection;
using Furion;

namespace MiRomWeb.Web.Entry
{
    public class SingleFilePublish : ISingleFilePublish
    {
        public Assembly[] IncludeAssemblies()
        {
            return Array.Empty<Assembly>();
        }

        public string[] IncludeAssemblyNames()
        {
            return new[]
            {
                "MiRomWeb.Application",
                "MiRomWeb.Core",
                "MiRomWeb.Web.Core"
            };
        }
    }
}