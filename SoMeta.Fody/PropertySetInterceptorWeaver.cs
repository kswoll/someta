using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Someta.Fody
{
    public class PropertySetInterceptorWeaver : BaseWeaver
    {
        private readonly MethodReference setPropertyValue;

        public PropertySetInterceptorWeaver(WeaverContext context, TypeReference propertyInterceptorInterface) : base(context)
        {
            setPropertyValue = ModuleDefinition.FindMethod(propertyInterceptorInterface, "SetPropertyValue");
        }

        public void Weave(PropertyDefinition property, ExtensionPointAttribute extensionPoint)
        {
//            if (property.DeclaringType != interceptor.DeclaringType)
//                Debugger.Launch();
            var type = property.DeclaringType;
            LogInfo($"Weaving property interceptor {extensionPoint.AttributeType.FullName} at {type.FullName}.{property.Name}");

            var propertyInfoField = property.CachePropertyInfo();
            var attributeField = CacheAttributeInstance(property, propertyInfoField, extensionPoint);

            LogInfo("Setter is intercepted");

            var method = property.SetMethod;
            var proceedReference = ImplementProceedSet(method, extensionPoint.AttributeType);

            // Re-implement method
            method.Body.Emit(il =>
            {
                ImplementSetBody(property, attributeField, propertyInfoField, method, il, proceedReference);
            });
        }

        private void ImplementSetBody(PropertyDefinition property, FieldDefinition attributeField, FieldDefinition propertyInfoField, MethodDefinition method, ILProcessor il, MethodReference proceed)
        {
            // We want to call the interceptor's setter method:
            // void SetPropertyValue(PropertyInfo propertyInfo, object instance, object oldValue, object newValue, Action<object> setter)

            // Get interceptor attribute
            il.LoadField(attributeField);

            // Leave PropertyInfo on the stack as the first argument
            il.LoadField(propertyInfoField);

            // Leave the instance on the stack as the second argument
            EmitInstanceArgument(il, method);

            // Get the current value of the property as the third argument
            il.EmitThisIfRequired(property.GetMethod);
            il.EmitCall(property.GetMethod);
            il.EmitBoxIfNeeded(method.Parameters[0].ParameterType);

            // Leave the new value on the stack as the fourth argument
            il.EmitArgument(method, 0);
            il.EmitBoxIfNeeded(method.Parameters[0].ParameterType);

            // Leave the delegate for the proceed implementation on the stack as fifth argument
            il.EmitLocalMethodDelegate(proceed, Context.Action1Type, TypeSystem.ObjectReference);

            // Finally, we emit the call to the interceptor
            il.Emit(OpCodes.Callvirt, setPropertyValue);

            // Return
            il.Emit(OpCodes.Ret);
        }

        private MethodReference ImplementProceedSet(MethodDefinition method, TypeReference interceptorAttribute)
        {
            var type = method.DeclaringType;
            var original = method.MoveImplementation(GenerateUniqueName(method, interceptorAttribute, "Original"));
            var proceed = method.CreateMethodThatMatchesStaticScope(GenerateUniqueName(method, interceptorAttribute, "Proceed"),
                MethodAttributes.Private, TypeSystem.VoidReference);

            proceed.Parameters.Add(new ParameterDefinition(TypeSystem.ObjectReference));

            MethodReference proceedReference = proceed;
            TypeReference genericType = type;
            if (type.HasGenericParameters)
            {
                genericType = type.MakeGenericInstanceType(type.GenericParameters.Concat(method.GenericParameters).ToArray());
                proceedReference = proceed.Bind((GenericInstanceType)genericType);
            }
            proceed.Body.Emit(il =>
            {
                var parameterInfos = original.Parameters;

                if (!method.IsStatic)
                {
                    // Load target for subsequent call
                    il.Emit(OpCodes.Ldarg_0);                    // Load "this"
                    il.Emit(OpCodes.Castclass, genericType);
                }

                var parameterInfo = parameterInfos[0];

                // Push object argument (setter.value)
                il.EmitArgument(method, 0);

                // If it's a value type, unbox it
                il.EmitUnboxIfNeeded(parameterInfo.ParameterType, type);

                var genericProceedTargetMethod = original.BindAll(type);
                il.EmitCall(genericProceedTargetMethod);
                il.Emit(OpCodes.Ret);
            });

            return proceedReference;
        }
    }
}