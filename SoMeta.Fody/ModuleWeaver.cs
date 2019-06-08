using System.Collections.Generic;
using Fody;

namespace SoMeta.Fody
{
    public class ModuleWeaver : BaseModuleWeaver
    {
        public override IEnumerable<string> GetAssembliesForScanning()
        {
            return new[] {"netstandard", "mscorlib"};
        }

        public override void Execute()
        {
            LogError("Test");
        }
    }
}