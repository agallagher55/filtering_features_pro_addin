using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace ProAppAddInSdeSearch
{
    internal class Module1 : Module
    {
        private static Module1 _this = null;

        public static Module1 Current =>
            _this ??= (Module1)FrameworkApplication.FindModule("ProAppAddInSdeSearch_Module");

        protected override bool CanUnload() => true;
    }
}
