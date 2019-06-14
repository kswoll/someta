using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Someta.Fody
{
    public class InstanceInitializerWeaver : BaseWeaver
    {
        private readonly MethodReference instanceInitializerInitialize;

        public InstanceInitializerWeaver(WeaverContext context) : base(context)
        {
            var instanceInitializerInterface = ModuleDefinition.FindType("Someta", "IInstanceInitializer", Context.Someta);
            instanceInitializerInitialize = ModuleDefinition.FindMethod(instanceInitializerInterface, "Initialize");
        }

        public void Weave(IMemberDefinition member, InterceptorAttribute interceptor)
        {
//            Debugger.Launch();

            var attributeField = CacheAttributeInstance(member, interceptor);
            FieldDefinition memberInfoField;
            if (member is TypeDefinition)
                memberInfoField = null;
            else
                memberInfoField = member.CacheMemberInfo();

            var type = member is TypeDefinition definition ? definition : member.DeclaringType;
            type    .EmitToConstructor(il =>
            {
                il.LoadField(attributeField);
                il.Emit(OpCodes.Ldarg_0);
                if (member is TypeDefinition)
                    il.LoadType(type);
                else
                    il.LoadField(memberInfoField);
                il.EmitCall(instanceInitializerInitialize);
            });
        }
    }
}