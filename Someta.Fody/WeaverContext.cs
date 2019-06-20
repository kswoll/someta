using System;
using System.Collections.Generic;
using Mono.Cecil;
using TypeSystem = Fody.TypeSystem;

namespace Someta.Fody
{
    public class WeaverContext
    {
        public static readonly TypeAttributes Struct = TypeAttributes.AnsiClass | TypeAttributes.SequentialLayout | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit;
        public static readonly MethodAttributes Constructor = MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;

        public ModuleDefinition ModuleDefinition { get; set; }
        public TypeSystem TypeSystem { get; set; }
        public AssemblyNameReference Someta { get; set; }
        public Action<string> LogInfo { get; set; }
        public Action<string> LogError { get; set; }
        public Action<string> LogWarning { get; set; }
        public TypeReference MethodInfoType { get; set; }
        public TypeReference PropertyInfoType { get; set; }
        public TypeReference EventInfoType { get; set; }
        public TypeReference Func1Type { get; set; }
        public TypeReference Func2Type { get; set; }
        public TypeReference Action1Type { get; set; }
        public TypeReference ObjectArrayType { get; set; }
        public TypeReference TaskType { get; set; }
        public TypeReference TaskTType { get; set; }
        public TypeReference AsyncTaskMethodBuilder { get; set; }
        public TypeReference DelegateType { get; set; }
        public List<TypeReference> ActionTypes  { get; set; }
        public List<TypeReference> FuncTypes { get; set; }
        public TypeReference ValueType { get; set; }

        public TypeReference MethodFinder { get; set; }
        public MethodReference FindMethod { get; set; }
        public MethodReference FindProperty { get; set; }
        public MethodReference OriginalMethodAttributeConstructor { get; set; }
    }
}