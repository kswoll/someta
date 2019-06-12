using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TypeSystem = Fody.TypeSystem;

namespace SoMeta.Fody
{
    public class ClassEnhancerWeaver : BaseWeaver
    {
        private TypeReference injectAccessAttribute;

        public ClassEnhancerWeaver(ModuleDefinition moduleDefinition, WeaverContext context, TypeSystem typeSystem, Action<string> logInfo, Action<string> logError, Action<string> logWarning) : base(moduleDefinition, context, typeSystem, logInfo, logError, logWarning)
        {
            injectAccessAttribute = moduleDefinition.FindType("SoMeta", "InjectAccessAttribute");
        }

        public void Weave(TypeDefinition type, CustomAttribute interceptor, int attributeIndex, InterceptorScope scope)
        {
            foreach (var interceptorProperty in interceptor.AttributeType.Resolve().Properties)
            {
                var injectAccess = interceptorProperty.CustomAttributes.SingleOrDefault(x => x.AttributeType.CompareTo(injectAccessAttribute));
                if (injectAccess != null)
                {
                    var methodName = (string)injectAccess.Properties.SingleOrDefault(x => x.Name == "PropertyName").Argument.Value;
                    var method = type.Methods.Single(x => x.Name == methodName);    // Todo: won't work for overloads

                    // Generate a private static method that forms the accessor that will be assigned
                    // to this property on the associated interceptor in a static initializer.
                    var accessor = new MethodDefinition($"<>__{methodName}$Accessor", MethodAttributes.Private | MethodAttributes.Static, method.ReturnType);
                    accessor.Parameters.Add(new ParameterDefinition(type));
                    foreach (var parameter in method.Parameters)
                    {
                        accessor.Parameters.Add(new ParameterDefinition(parameter.ParameterType));
                    }

                    var delegateType = new TypeDefinition(type.Namespace, accessor.Name + "Type", TypeAttributes.NestedPrivate, Context.DelegateType);

                    method.Body = new MethodBody(method);
                    method.Body.InitLocals = true;
                    method.Body.Emit(il =>
                    {
                        for (var i = 0; i < accessor.Parameters.Count; i++)
                        {
                            il.EmitArgument(method, i);
                        }
                        il.EmitCall(method);
                    });

                    // Now set up the static initializer to assign this to the interceptor property
                    type.EmitStaticConstructor(il =>
                    {
                        il.EmitGetAttributeByIndex(type, attributeIndex, interceptor.AttributeType);
                        il.EmitDelegate(accessor, )
                        il.Emit(OpCodes.Ldnull);
                        il.Emit(OpCodes.Ldftn, accessor);
                        il.Emit(OpCodes.Newobj, );
                        il.EmitCall(interceptorProperty.SetMethod);
                    });
                }
            }
        }
    }
}