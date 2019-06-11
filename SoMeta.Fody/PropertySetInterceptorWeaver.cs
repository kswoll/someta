using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using TypeSystem = Fody.TypeSystem;

namespace SoMeta.Fody
{
    public class PropertySetInterceptorWeaver : BaseWeaver
    {
        private readonly MethodReference baseSetPropertyValue;

        public PropertySetInterceptorWeaver(ModuleDefinition moduleDefinition, WeaverContext context, TypeSystem typeSystem, Action<string> logInfo, Action<string> logError, Action<string> logWarning, TypeReference propertyInterceptorInterface) :
            base(moduleDefinition, context, typeSystem, logInfo, logError, logWarning)
        {
            baseSetPropertyValue = moduleDefinition.FindMethod(propertyInterceptorInterface, "SetPropertyValue");
        }

        public void Weave(PropertyDefinition property, CustomAttribute interceptor)
        {
            var type = property.DeclaringType;
            LogInfo($"Weaving property interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{property.Name}");

            var propertyInfoField = property.CachePropertyInfo();

            LogInfo("Setter is intercepted");

            var method = property.SetMethod;
            var proceedReference = ImplementProceedSet(method);

            // Re-implement method
            method.Body.Emit(il =>
            {
                ImplementSetBody(property, propertyInfoField, method, il, proceedReference, interceptor.AttributeType);
            });
        }

        private void ImplementSetBody(PropertyDefinition property, FieldDefinition propertyInfoField, MethodDefinition method, ILProcessor il, MethodReference proceed, TypeReference interceptorAttribute)
        {
            // We want to call the interceptor's setter method:
            // void SetPropertyValue(PropertyInfo propertyInfo, object instance, object newValue, Action<object> setter)

            // Get interceptor attribute
            il.EmitGetAttribute(propertyInfoField, interceptorAttribute);

            // Leave PropertyInfo on the stack as the first argument
            il.EmitGetPropertyInfo(property);

            // Leave the instance on the stack as the second argument
            EmitInstanceArgument(il, method);

            // Leave the new value on the stack as the third argument
            if (method.IsStatic)
                il.Emit(OpCodes.Ldarg_0);
            else
                il.Emit(OpCodes.Ldarg_1);

            il.EmitBoxIfNeeded(method.Parameters[0].ParameterType);

            // Leave the delegate for the proceed implementation on the stack as the fourth argument
            il.EmitDelegate(proceed, Context.Action1Type, TypeSystem.ObjectReference);

            // Finally, we emit the call to the interceptor
            il.Emit(OpCodes.Callvirt, baseSetPropertyValue);

            // Return
            il.Emit(OpCodes.Ret);
        }

        private MethodReference ImplementProceedSet(MethodDefinition method)
        {
            var type = method.DeclaringType;
            var original = method.MoveImplementation($"{method.Name}$Original");
            var proceed = method.CreateSimilarMethod($"{method.Name}$Proceed", MethodAttributes.Private, TypeSystem.VoidReference);
            proceed.Parameters.Add(new ParameterDefinition(TypeSystem.ObjectReference));

            MethodReference proceedReference = proceed;
            if (type.HasGenericParameters)
            {
                proceedReference = proceed.Bind(type.MakeGenericInstanceType(type.GenericParameters.Concat(method.GenericParameters).ToArray()));
            }
            proceed.Body.Emit(il =>
            {
                var parameterInfos = original.Parameters;

                if (!method.IsStatic)
                {
                    // Load target for subsequent call
                    il.Emit(OpCodes.Ldarg_0);                    // Load "this"
                    il.Emit(OpCodes.Castclass, original.DeclaringType);
                }

                var parameterInfo = parameterInfos[0];

                // Push object argument (setter.value)
                if (!method.IsStatic)
                {
                    il.Emit(OpCodes.Ldarg_1);
                }
                else
                {
                    il.Emit(OpCodes.Ldarg_0);
                }

                // If it's a value type, unbox it
                il.EmitUnboxIfNeeded(parameterInfo.ParameterType, type);

                var genericProceedTargetMethod = original.BindAll(type);
                if (method.IsStatic)
                {
                    il.Emit(OpCodes.Call, genericProceedTargetMethod);
                }
                else
                {
                    il.Emit(OpCodes.Callvirt, genericProceedTargetMethod);
                }
                il.Emit(OpCodes.Ret);
            });

            return proceedReference;
        }
        
    }
}