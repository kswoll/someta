using System;
using Mono.Cecil;
using TypeSystem = Fody.TypeSystem;

namespace SoMeta.Fody
{
    public class BaseWeaver
    {
        public ModuleDefinition ModuleDefinition { get; set; }
        public WeaverContext Context { get; set; }
        public TypeSystem TypeSystem { get; set; }
        public Action<string> LogInfo { get; set; }
        public Action<string> LogError { get; set; }
        public Action<string> LogWarning { get; set; }

        public BaseWeaver(ModuleDefinition moduleDefinition, WeaverContext context, TypeSystem typeSystem, Action<string> logInfo, Action<string> logError, Action<string> logWarning)
        {
            ModuleDefinition = moduleDefinition;
            Context = context;
            TypeSystem = typeSystem;
            LogInfo = logInfo;
            LogError = logError;
            LogWarning = logWarning;
        }
    }
}