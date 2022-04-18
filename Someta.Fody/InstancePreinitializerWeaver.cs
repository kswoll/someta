using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Someta.Fody
{
    /// <summary>
    /// Similar to InstanceInitializerWeaver except happens at the beginning of the constructor instead of the end.
    /// </summary>
    public class InstancePreinitializerWeaver : BaseWeaver
    {
        private readonly MethodReference instanceInitializerInitialize;

        public InstancePreinitializerWeaver(WeaverContext context) : base(context)
        {
            var instanceInitializerInterface = FindType("Someta", "IInstancePreinitializer");
            instanceInitializerInitialize = FindMethod(instanceInitializerInterface, "Preinitialize");
        }

        public void Weave(IMemberDefinition member, ExtensionPointAttribute extensionPoint)
        {
//            Debugger.Launch();

            var attributeField = CacheAttributeInstance(member, extensionPoint);
            FieldDefinition memberInfoField;
            if (member is TypeDefinition)
                memberInfoField = null;
            else
                memberInfoField = member.CacheMemberInfo();

            var type = member is TypeDefinition definition ? definition : member.DeclaringType;
            type.EmitToConstructorStart(il =>
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