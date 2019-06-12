using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using TypeSystem = Fody.TypeSystem;

namespace SoMeta.Fody
{
    public class PropertyGetInterceptorWeaver : BaseWeaver
    {
        private readonly MethodReference baseGetPropertyValue;

        public PropertyGetInterceptorWeaver(ModuleDefinition moduleDefinition, WeaverContext context, TypeSystem typeSystem, Action<string> logInfo, Action<string> logError, Action<string> logWarning, TypeReference propertyInterceptorInterface) :
            base(moduleDefinition, context, typeSystem, logInfo, logError, logWarning)
        {
            baseGetPropertyValue = moduleDefinition.FindMethod(propertyInterceptorInterface, "GetPropertyValue");
        }

        public void Weave(PropertyDefinition property, CustomAttribute interceptor, int attributeIndex, InterceptorScope scope)
        {
            var type = property.DeclaringType;
            LogInfo($"Weaving property get interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{property.Name}");

            var propertyInfoField = property.CachePropertyInfo();
            var attributeField = CacheAttributeInstance(property, propertyInfoField, interceptor.AttributeType, attributeIndex, scope);

            var method = property.GetMethod;
            var proceedReference = ImplementProceedGet(method, interceptor.AttributeType);

            // Re-implement method
            method.Body.Emit(il =>
            {
                ImplementGetBody(attributeField, propertyInfoField, method, il, proceedReference);
            });
        }

        private void ImplementGetBody(FieldDefinition attributeField, FieldDefinition propertyInfoField, MethodDefinition method, ILProcessor il, MethodReference proceed)
        {
            // We want to call the interceptor's setter method:
            // void GetPropertyValue(PropertyInfo propertyInfo, object instance, Action<object> getter)

            // Get interceptor attribute
            il.Emit(OpCodes.Ldsfld, attributeField);

            // Leave PropertyInfo on the stack as the first argument
            il.Emit(OpCodes.Ldsfld, propertyInfoField);

            // Leave the instance on the stack as the second argument
            EmitInstanceArgument(il, method);

            // Leave the delegate for the proceed implementation on the stack as the third argument
            il.EmitDelegate(proceed, Context.Func1Type, TypeSystem.ObjectReference);

            // Finally, we emit the call to the interceptor
            il.Emit(OpCodes.Callvirt, baseGetPropertyValue);

            // Now unbox the value if necessary
            il.EmitUnboxIfNeeded(method.ReturnType, method.DeclaringType);

            // Return
            il.Emit(OpCodes.Ret);
        }

        private MethodReference ImplementProceedGet(MethodDefinition method, TypeReference interceptorAttribute)
        {
            var type = method.DeclaringType;
            var original = method.MoveImplementation(GenerateUniqueName(method, interceptorAttribute, "Original"));
            var proceed = method.CreateSimilarMethod(GenerateUniqueName(method, interceptorAttribute, "Proceed"),
                MethodAttributes.Private, method.ReturnType);

            MethodReference proceedReference = proceed;
            if (type.HasGenericParameters)
            {
                proceedReference = proceed.Bind(type.MakeGenericInstanceType(type.GenericParameters.Concat(method.GenericParameters).ToArray()));
            }
            proceed.Body.Emit(il =>
            {
                if (!method.IsStatic)
                {
                    // Load target for subsequent call
                    il.Emit(OpCodes.Ldarg_0);                    // Load "this"
                    il.Emit(OpCodes.Castclass, original.DeclaringType);
                }

                var genericProceedTargetMethod = original.BindAll(type);
                if (method.IsStatic)
                {
                    il.Emit(OpCodes.Call, genericProceedTargetMethod);
                }
                else
                {
                    il.Emit(OpCodes.Callvirt, genericProceedTargetMethod);
                }

                // If it's a value type, box it
                il.EmitBoxIfNeeded(method.ReturnType);

                il.Emit(OpCodes.Ret);
            });

            return proceedReference;
        }
    }
}