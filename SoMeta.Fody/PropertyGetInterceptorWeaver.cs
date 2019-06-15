using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Someta.Fody
{
    public class PropertyGetInterceptorWeaver : BaseWeaver
    {
        private readonly MethodReference baseGetPropertyValue;

        public PropertyGetInterceptorWeaver(WeaverContext context, TypeReference propertyInterceptorInterface) : base(context)
        {
            baseGetPropertyValue = ModuleDefinition.FindMethod(propertyInterceptorInterface, "GetPropertyValue");
        }

        public void Weave(PropertyDefinition property, ExtensionPointAttribute extensionPoint)
        {
            var type = property.DeclaringType;
            LogInfo($"Weaving property get interceptor {extensionPoint.AttributeType.FullName} at {type.FullName}.{property.Name}");

            var propertyInfoField = property.CachePropertyInfo();
            var attributeField = CacheAttributeInstance(property, propertyInfoField, extensionPoint);

            var method = property.GetMethod;
            var proceedReference = ImplementProceedGet(method, extensionPoint.AttributeType);

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
            il.LoadField(attributeField);

            // Leave PropertyInfo on the stack as the first argument
            il.LoadField(propertyInfoField);

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
                MethodAttributes.Private, TypeSystem.ObjectReference);

            MethodReference proceedReference = proceed;
            TypeReference genericType = type;
            if (type.HasGenericParameters)
            {
                genericType = type.MakeGenericInstanceType(type.GenericParameters.Concat(method.GenericParameters).ToArray());
                proceedReference = proceed.Bind(type.MakeGenericInstanceType(type.GenericParameters.Concat(method.GenericParameters).ToArray()));
            }
            proceed.Body.Emit(il =>
            {
                if (!method.IsStatic)
                {
                    // Load target for subsequent call
                    il.Emit(OpCodes.Ldarg_0);                    // Load "this"
                    il.Emit(OpCodes.Castclass, genericType);
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