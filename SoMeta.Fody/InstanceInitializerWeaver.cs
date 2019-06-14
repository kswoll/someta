using System;
using System.Diagnostics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TypeSystem = Fody.TypeSystem;

namespace Someta.Fody
{
    public class InstanceInitializerWeaver : BaseWeaver
    {
        private readonly MethodReference instanceInitializerInitialize;

        public InstanceInitializerWeaver(ModuleDefinition moduleDefinition, WeaverContext context, TypeSystem typeSystem, Action<string> logInfo, Action<string> logError, Action<string> logWarning) : base(moduleDefinition, context, typeSystem, logInfo, logError, logWarning)
        {
            var instanceInitializerInterface = ModuleDefinition.FindType("Someta", "IInstanceInitializer", Context.Someta);
            instanceInitializerInitialize = ModuleDefinition.FindMethod(instanceInitializerInterface, "Initialize");
        }

        public void Weave(IMemberDefinition member, InterceptorAttribute interceptor)
        {
//            Debugger.Launch();

            var attributeField = CacheAttributeInstance(member, interceptor);

            member.DeclaringType.EmitToConstructor(il =>
            {
                il.LoadField(attributeField);
                il.Emit(OpCodes.Ldarg_0);
                il.EmitCall(instanceInitializerInitialize);
            });
        }
    }
}