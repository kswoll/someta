using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Someta.Fody
{
    public class ClassEnhancerWeaver : BaseWeaver
    {
        private readonly TypeReference injectAccessAttribute;
        private readonly TypeReference injectTargetAttribute;

        public ClassEnhancerWeaver(WeaverContext context) : base(context)
        {
            injectAccessAttribute = ModuleDefinition.FindType("Someta", "InjectAccessAttribute");
            injectTargetAttribute = ModuleDefinition.FindType("Someta", "InjectTargetAttribute");
        }

        public void Weave(TypeDefinition type, ExtensionPointAttribute extensionPoint)
        {
//            Debugger.Launch();

            LogInfo($"Weaving class enhancer {extensionPoint.AttributeType.FullName} at {type.FullName}");
//            interceptor.

            // For now, we don't want to impact subclasses since there's no current use case for that.  If that changes,
            // we'll revisit.
            if (type != extensionPoint.DeclaringType)
                return;

            var attributeField = CacheAttributeInstance(type, extensionPoint);

            foreach (var interceptorProperty in extensionPoint.AttributeType.Resolve().Properties)
            {
                var injectAccess = interceptorProperty.CustomAttributes.SingleOrDefault(x => x.AttributeType.CompareTo(injectAccessAttribute));
                if (injectAccess != null)
                {
//                    Debugger.Launch();
                    var key = (string)injectAccess.ConstructorArguments.Single().Value;

                    // Find target method.  First try using attributes:
                    var targetMethod = type.Methods.SingleOrDefault(x => x.CustomAttributes
                        .Any(y => injectTargetAttribute.IsAssignableFrom(y.AttributeType) && (string)y.ConstructorArguments[0].Value == key));
                    if (targetMethod == null)
                        targetMethod = type.Methods.Single(x => x.Name == key);    // Todo: won't work for overloads

                    // Generate a private static method that forms the accessor that will be assigned
                    // to this property on the associated interceptor in a static initializer.
                    var accessor = new MethodDefinition($"<>__{key}$Accessor", MethodAttributes.Private | MethodAttributes.Static, targetMethod.ReturnType);
                    accessor.Parameters.Add(new ParameterDefinition(TypeSystem.ObjectReference));
                    foreach (var parameter in targetMethod.Parameters)
                    {
                        accessor.Parameters.Add(new ParameterDefinition(parameter.ParameterType));
                    }

//                    var delegateType = new TypeDefinition(type.Namespace, accessor.Name + "Type", TypeAttributes.NestedPrivate, Context.DelegateType);
//                    delegateType.Methods.Add(new MethodDefinition("Invoke", MethodAttributes.Public, ))

                    accessor.Body = new MethodBody(accessor);
                    accessor.Body.InitLocals = true;
                    accessor.Body.Emit(il =>
                    {
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Castclass, type);

                        for (var i = 1; i <= targetMethod.Parameters.Count; i++)
                        {
                            il.EmitArgument(accessor, i);
                        }
                        il.EmitCall(targetMethod);
                        il.Emit(OpCodes.Ret);
                    });
                    type.Methods.Add(accessor);

                    // Now set up the static initializer to assign this to the interceptor property
                    type.EmitToStaticConstructor(il =>
                    {
                        var isVoid = targetMethod.ReturnType.CompareTo(TypeSystem.VoidReference);
                        var delegateType = (isVoid ? Context.ActionTypes : Context.FuncTypes)[targetMethod.Parameters.Count + 1];
                        var typeArguments = new List<TypeReference>();
                        typeArguments.Add(TypeSystem.ObjectReference);
                        typeArguments.AddRange(targetMethod.Parameters.Select(x => x.ParameterType));
                        if (!isVoid)
                            typeArguments.Add(targetMethod.ReturnType);

                        il.LoadField(attributeField);
                        il.EmitLocalMethodDelegate(accessor, delegateType, typeArguments.ToArray());
                        il.EmitCall(interceptorProperty.SetMethod);
                    });
                }
            }
        }
    }
}