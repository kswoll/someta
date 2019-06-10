using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using TypeSystem = Fody.TypeSystem;

namespace SoMeta.Fody
{
    public class PropertyInterceptorWeaver : BaseWeaver
    {
        private TypeReference propertyInterceptorAttribute;
        private MethodReference baseGetPropertyValue;
        private MethodReference baseSetPropertyValue;

        public PropertyInterceptorWeaver(ModuleDefinition moduleDefinition, WeaverContext context, TypeSystem typeSystem, Action<string> logInfo, Action<string> logError, Action<string> logWarning, TypeReference propertyInterceptorAttribute) :
            base(moduleDefinition, context, typeSystem, logInfo, logError, logWarning)
        {
            this.propertyInterceptorAttribute = propertyInterceptorAttribute;
            baseGetPropertyValue = moduleDefinition.FindMethod(propertyInterceptorAttribute, "GetPropertyValue");
            baseSetPropertyValue = moduleDefinition.FindMethod(propertyInterceptorAttribute, "SetPropertyValue");
        }

        public void Weave(PropertyDefinition property, CustomAttribute interceptor)
        {
            var type = property.DeclaringType;
            LogInfo($"Weaving property interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{property.Name}");

            // Check to see if the get interceptor is overridden
            var attributeType = interceptor.AttributeType.Resolve();

            var interceptsGetter = attributeType.IsMethodOverridden(baseGetPropertyValue);
            var interceptsSetter = attributeType.IsMethodOverridden(baseSetPropertyValue);

            if (interceptsGetter || interceptsSetter)
            {
                var propertyInfoField = property.CachePropertyInfo();

                if (interceptsGetter)
                {
                    LogInfo("Getter is intercepted");

                    var method = property.GetMethod;
                    var proceedReference = ImplementProceedGet(method);

                    // Re-implement method
                    method.Body.Emit(il =>
                    {
                        ImplementGetBody(property, propertyInfoField, method, il, proceedReference);
                    });
                }

                if (interceptsSetter)
                {
                    LogInfo("Setter is intercepted");

                    var method = property.SetMethod;
                    var proceedReference = ImplementProceedSet(method);

                    // Re-implement method
                    method.Body.Emit(il =>
                    {
                        ImplementSetBody(property, propertyInfoField, method, il, proceedReference);
                    });
                }
            }
            else
            {
                LogWarning("Interceptor intercepts neither the getter nor the setter");
            }
        }

        private void ImplementGetBody(PropertyDefinition property, FieldDefinition propertyInfoField, MethodDefinition method, ILProcessor il, MethodReference proceed)
        {
            // We want to call the interceptor's setter method:
            // void GetPropertyValue(PropertyInfo propertyInfo, object instance, Action<object> getter)

            // Get interceptor attribute
            il.EmitGetAttribute(propertyInfoField, propertyInterceptorAttribute);

            // Leave PropertyInfo on the stack as the first argument
            il.EmitGetPropertyInfo(property);

            if (!method.IsStatic)
            {
                // Leave instance (this) on the stack as the second argument
                il.Emit(OpCodes.Ldarg_0);
            }
            else
            {
                il.Emit(OpCodes.Ldnull);
            }

            // Leave the delegate for the proceed implementation on the stack as the fourth argument
            il.EmitDelegate(proceed, Context.Func1Type, TypeSystem.ObjectReference);

            // Finally, we emit the call to the interceptor
            il.Emit(OpCodes.Callvirt, baseGetPropertyValue);

            // Now unbox the value if necessary
            il.EmitUnboxIfNeeded(method.ReturnType, method.DeclaringType);

            // Return
            il.Emit(OpCodes.Ret);
        }

        private void ImplementSetBody(PropertyDefinition property, FieldDefinition propertyInfoField, MethodDefinition method, ILProcessor il, MethodReference proceed)
        {
            // We want to call the interceptor's setter method:
            // void SetPropertyValue(PropertyInfo propertyInfo, object instance, object newValue, Action<object> setter)

            // Get interceptor attribute
            il.EmitGetAttribute(propertyInfoField, propertyInterceptorAttribute);

            // Leave PropertyInfo on the stack as the first argument
            il.EmitGetPropertyInfo(property);

            if (!method.IsStatic)
            {
                // Leave instance (this) on the stack as the second argument
                il.Emit(OpCodes.Ldarg_0);
            }
            else
            {
                il.Emit(OpCodes.Ldnull);
            }

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