using System;
using Mono.Cecil;

namespace SoMeta.Fody
{
    public class WeaverContext
    {
        public ModuleDefinition ModuleDefinition { get; set; }
        public Action<string> LogInfo { get; set; }
        public Action<string> LogError { get; set; }
        public Action<string> LogWarning { get; set; }
        public TypeReference MethodInfoType { get; set; }
        public TypeReference PropertyInfoType { get; set; }
        public TypeReference Func1Type { get; set; }
        public TypeReference Func2Type { get; set; }
        public TypeReference Action1Type { get; set; }
        public TypeReference ObjectArrayType { get; set; }
        public TypeReference TaskType { get; set; }
        public TypeReference TaskTType { get; set; }
        public TypeReference AsyncTaskMethodBuilder { get; set; }
        public TypeReference DelegateType { get; set; }

        public TypeReference MethodFinder { get; set; }
        public MethodReference FindMethod { get; set; }
        public MethodReference FindProperty { get; set; }
        public MethodReference OriginalMethodAttributeConstructor { get; set; }
    }
}