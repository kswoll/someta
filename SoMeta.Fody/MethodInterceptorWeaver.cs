using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using TypeSystem = Fody.TypeSystem;

namespace SoMeta.Fody
{
    public class MethodInterceptorWeaver : BaseWeaver
    {
        private TypeReference methodInterceptorAttribute;
        private MethodReference baseInvoke;
        private MethodReference baseInvokeAsync;

        public MethodInterceptorWeaver(ModuleDefinition moduleDefinition, WeaverContext context, TypeSystem typeSystem, Action<string> logInfo, Action<string> logError, Action<string> logWarning, TypeReference methodInterceptorAttribute) :
            base(moduleDefinition, context, typeSystem, logInfo, logError, logWarning)
        {
            this.methodInterceptorAttribute = methodInterceptorAttribute;
            baseInvoke = moduleDefinition.FindMethod(methodInterceptorAttribute, "InvokeMethod");
        }

        public void Weave(MethodDefinition method, CustomAttribute interceptor)
        {
            LogInfo($"Weaving method interceptor {interceptor.AttributeType.FullName} at {method.Describe()}");

            // Check to see if the get interceptor is overridden
            var attributeType = interceptor.AttributeType.Resolve();

            var isIntercepted = attributeType.IsMethodOverridden(baseInvoke);

            if (isIntercepted)
            {
                var methodInfoField = method.CachePropertyInfo();

                LogInfo("Getter is intercepted");

                var proceedReference = ImplementProceedGet(method);

                // Re-implement method
                method.Body.Emit(il =>
                {
                    ImplementGetBody(property, propertyInfoField, method, il, proceedReference);
                });
            }
            else
            {
                LogWarning("Interceptor does not override any intercept methods");
            }
        }


        private void ImplementGetBody(PropertyDefinition property, FieldDefinition propertyInfoField, MethodDefinition method, ILProcessor il, MethodReference proceed)
        {
            // We want to call the interceptor's setter method:
            // void GetPropertyValue(PropertyInfo propertyInfo, object instance, Action<object> getter)

            // Get interceptor attribute
            il.EmitGetAttribute(propertyInfoField, propertyInterceptorAttribute);

            // Leave PropertyInfo on the stack as the first argument
            il.EmitGetProperty(property);

            // Leave instance (this) on the stack as the second argument
            il.Emit(OpCodes.Ldarg_0);

            // Leave the delegate for the proceed implementation on the stack as the fourth argument
            il.EmitDelegate(proceed, Context.Func1Type);

            // Finally, we emit the call to the interceptor
            il.Emit(OpCodes.Callvirt, baseGetPropertyValue);

            // Now unbox the value if necessary
            il.EmitUnboxIfNeeded(method.ReturnType, method.DeclaringType);

            // Return
            il.Emit(OpCodes.Ret);
        }

        private MethodReference ImplementProceedGet(MethodDefinition method)
        {
            var type = method.DeclaringType;
            var original = method.MoveImplementation($"{method.Name}$Original");
            var proceed = method.CreateSimilarMethod($"{method.Name}$Proceed", MethodAttributes.Private, method.ReturnType);

            MethodReference proceedReference = proceed;
            if (type.HasGenericParameters)
            {
                proceedReference = proceed.Bind(type.MakeGenericInstanceType(type.GenericParameters.Concat(method.GenericParameters).ToArray()));
            }
            proceed.Body.Emit(il =>
            {
                // Load target for subsequent call
                il.Emit(OpCodes.Ldarg_0);                    // Load "this"
                il.Emit(OpCodes.Castclass, original.DeclaringType);

                var genericProceedTargetMethod = original.BindAll(type);
                il.Emit(OpCodes.Callvirt, genericProceedTargetMethod);

                // If it's a value type, box it
                il.EmitBoxIfNeeded(method.ReturnType);

                il.Emit(OpCodes.Ret);
            });

            return proceedReference;
        }
    }
}